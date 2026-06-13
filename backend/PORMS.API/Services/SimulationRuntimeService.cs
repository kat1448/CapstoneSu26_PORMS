using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Simulation;
using PORMS.Application.Services.Risk;
using PORMS.Application.Services.Simulation;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Infrastructure.Weather;

namespace PORMS.API.Services;

public sealed class SimulationRuntimeService : ISimulationService
{
    private const int MinimumSnapshots = 5;
    private const int BaseSnapshotIntervalSeconds = 15 * 60;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SimulationRuntimeService> _logger;
    private readonly ConcurrentDictionary<Guid, SimulationRuntimeState> _runtime = new();

    public SimulationRuntimeService(
        IServiceScopeFactory scopeFactory,
        ILogger<SimulationRuntimeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SimulationSessionDto> StartAsync(
        StartSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateStartRequest(request);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var portExists = await dbContext.Ports
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.PortId && x.IsActive, cancellationToken);
        if (!portExists)
        {
            throw new KeyNotFoundException($"Port {request.PortId} was not found or is inactive.");
        }

        var startedByUserId = request.StartedByUserId
            ?? await dbContext.Users
                .AsNoTracking()
                .Where(x => x.DeletedAt == null && x.Status == UserStatus.ACTIVE)
                .OrderBy(x => x.Role)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (startedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Simulation requires at least one active user as starter.");
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == startedByUserId && x.DeletedAt == null, cancellationToken);
        if (!userExists)
        {
            throw new KeyNotFoundException($"Starter user {startedByUserId} was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var session = new SimulationSession
        {
            Id = Guid.NewGuid(),
            PortId = request.PortId,
            StartedByUserId = startedByUserId,
            ScenarioName = request.ScenarioName.Trim(),
            SpeedMultiplier = request.SpeedMultiplier,
            TotalSnapshots = request.WeatherSnapshots.Count,
            Status = "RUNNING",
            StartedAt = now
        };

        dbContext.SimulationSessions.Add(session);
        dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = session.PortId,
            EventType = OperationEventType.SIMULATION_STARTED,
            ActorUserId = startedByUserId,
            Payload = JsonSerializer.Serialize(new
            {
                sessionId = session.Id,
                session.ScenarioName,
                session.SpeedMultiplier,
                session.TotalSnapshots
            }),
            Summary = $"Simulation started: {session.ScenarioName}.",
            OccurredAt = now,
            IsSimulation = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var runtimeState = new SimulationRuntimeState(
            request.WeatherSnapshots.ToArray(),
            request.SpeedMultiplier);
        _runtime[session.Id] = runtimeState;

        _ = Task.Run(
            () => ReplayAsync(session.Id, runtimeState),
            CancellationToken.None);

        return ToDto(session);
    }

    public async Task<SimulationSessionDto> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var session = await dbContext.SimulationSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Simulation session {sessionId} was not found.");

        if (session.Status == "RUNNING")
        {
            session.Status = "CANCELLED";
            session.EndedAt = DateTimeOffset.UtcNow;
            dbContext.OperationEvents.Add(CreateSimulationEndedEvent(session, "Simulation cancelled by request."));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (_runtime.TryGetValue(sessionId, out var runtimeState))
        {
            await runtimeState.CancelAsync();
        }

        return ToDto(session);
    }

    public async Task<SimulationStatusDto?> GetStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var session = await dbContext.SimulationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        _runtime.TryGetValue(sessionId, out var runtimeState);
        var completed = runtimeState?.CompletedSnapshots
            ?? await CountSimulationReadingsAsync(dbContext, session, cancellationToken);
        var current = runtimeState?.CurrentSnapshotNumber;
        var currentWeather = runtimeState?.CurrentWeather;
        var percent = session.TotalSnapshots == 0
            ? 0
            : Math.Round(completed * 100m / session.TotalSnapshots, 2, MidpointRounding.AwayFromZero);
        var remaining = session.Status == "RUNNING" && runtimeState is not null
            ? (TimeSpan?)EstimateRemaining(session.TotalSnapshots, completed, session.SpeedMultiplier)
            : null;

        return new SimulationStatusDto(
            session.Id,
            session.PortId,
            session.ScenarioName,
            session.Status,
            session.SpeedMultiplier,
            session.TotalSnapshots,
            completed,
            percent,
            current,
            currentWeather,
            session.StartedAt,
            session.EndedAt,
            remaining);
    }

    public async Task<SimulationResultsDto?> GetResultsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var session = await dbContext.SimulationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var from = session.StartedAt;
        var to = session.EndedAt ?? DateTimeOffset.UtcNow;

        var assessmentsQuery = dbContext.RiskAssessments
            .AsNoTracking()
            .Where(x => x.PortId == session.PortId &&
                        x.IsSimulation &&
                        x.EvaluatedAt >= from &&
                        x.EvaluatedAt <= to);

        var riskDistribution = await assessmentsQuery
            .GroupBy(x => x.FinalRiskLevel)
            .Select(x => new { RiskLevel = x.Key, Count = x.Count() })
            .ToDictionaryAsync(
                x => x.RiskLevel.ToString(),
                x => x.Count,
                cancellationToken);

        var peakRisk = await assessmentsQuery
            .OrderByDescending(x => x.FinalRiskLevel)
            .Select(x => (RiskLevel?)x.FinalRiskLevel)
            .FirstOrDefaultAsync(cancellationToken);

        return new SimulationResultsDto(
            session.Id,
            session.PortId,
            session.ScenarioName,
            session.Status,
            session.TotalSnapshots,
            await dbContext.WeatherReadings.CountAsync(x =>
                x.PortId == session.PortId &&
                x.IsSimulation &&
                x.RecordedAt >= from &&
                x.RecordedAt <= to,
                cancellationToken),
            await assessmentsQuery.CountAsync(cancellationToken),
            await assessmentsQuery.CountAsync(x => x.LevelChanged, cancellationToken),
            await dbContext.SopExecutions.CountAsync(x =>
                x.PortId == session.PortId &&
                x.IsSimulation &&
                x.ExecutedAt >= from &&
                x.ExecutedAt <= to,
                cancellationToken),
            await dbContext.Alerts.CountAsync(x =>
                x.PortId == session.PortId &&
                x.IsSimulation &&
                x.CreatedAt >= from &&
                x.CreatedAt <= to,
                cancellationToken),
            await dbContext.TaskLogs.CountAsync(x =>
                x.PortId == session.PortId &&
                x.IsSimulation &&
                x.CreatedAt >= from &&
                x.CreatedAt <= to,
                cancellationToken),
            peakRisk,
            riskDistribution,
            session.StartedAt,
            session.EndedAt);
    }

    private async Task ReplayAsync(Guid sessionId, SimulationRuntimeState runtimeState)
    {
        try
        {
            for (var i = 0; i < runtimeState.Snapshots.Count; i++)
            {
                runtimeState.CancellationToken.ThrowIfCancellationRequested();

                await ReplaySnapshotAsync(
                    sessionId,
                    runtimeState,
                    snapshotNumber: i + 1,
                    runtimeState.Snapshots[i],
                    runtimeState.CancellationToken);

                runtimeState.CompletedSnapshots = i + 1;

                if (i < runtimeState.Snapshots.Count - 1)
                {
                    await Task.Delay(
                        GetReplayDelay(runtimeState.SpeedMultiplier),
                        runtimeState.CancellationToken);
                }
            }

            await CompleteSessionAsync(sessionId, "COMPLETED", "Simulation completed.");
        }
        catch (OperationCanceledException)
        {
            await CompleteSessionAsync(sessionId, "CANCELLED", "Simulation cancelled.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Simulation {SessionId} failed during replay.", sessionId);
            await CompleteSessionAsync(sessionId, "CANCELLED", "Simulation cancelled because replay failed.");
        }
        finally
        {
            _runtime.TryRemove(sessionId, out var state);
            if (state is not null)
            {
                await state.DisposeAsync();
            }
        }
    }

    private async Task ReplaySnapshotAsync(
        Guid sessionId,
        SimulationRuntimeState runtimeState,
        int snapshotNumber,
        SimulationWeatherSnapshotDto snapshot,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var riskEngine = scope.ServiceProvider.GetRequiredService<IRiskEngine>();

        var session = await dbContext.SimulationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Simulation session {sessionId} was not found.");
        if (session.Status != "RUNNING")
        {
            throw new OperationCanceledException(cancellationToken);
        }

        runtimeState.CurrentSnapshotNumber = snapshotNumber;
        runtimeState.CurrentWeather = snapshot;

        var now = DateTimeOffset.UtcNow;
        var reading = new WeatherReading
        {
            Id = Guid.NewGuid(),
            PortId = session.PortId,
            WindSpeedMs = snapshot.WindSpeedMs,
            BeaufortNumber = OpenWeatherService.ConvertToBeaufort(snapshot.WindSpeedMs),
            WindDirectionDeg = snapshot.WindDirectionDeg,
            WindGustMs = snapshot.WindGustMs,
            Rainfall1hMm = snapshot.Rainfall1hMm,
            Rainfall3hMm = snapshot.Rainfall3hMm,
            VisibilityKm = snapshot.VisibilityKm,
            TemperatureC = snapshot.TemperatureC,
            HumidityPct = snapshot.HumidityPct,
            PressureHpa = snapshot.PressureHpa,
            OpenWeatherCode = 800,
            OpenWeatherDescription = "simulation snapshot",
            OpenWeatherIcon = "sim",
            ObservedAt = snapshot.ObservedAt,
            RecordedAt = now,
            DataSource = "SIMULATION",
            RawPayload = JsonSerializer.Serialize(new
            {
                sessionId,
                snapshotNumber,
                totalSnapshots = session.TotalSnapshots,
                scenarioName = session.ScenarioName,
                snapshot
            }),
            IsSimulation = true
        };

        dbContext.WeatherReadings.Add(reading);
        dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = session.PortId,
            EventType = OperationEventType.WEATHER_FETCHED,
            Payload = JsonSerializer.Serialize(new
            {
                sessionId,
                snapshotNumber,
                readingId = reading.Id,
                reading.WindSpeedMs,
                reading.BeaufortNumber,
                reading.Rainfall1hMm,
                reading.VisibilityKm,
                reading.ObservedAt
            }),
            Summary = $"Simulation weather snapshot {snapshotNumber}/{session.TotalSnapshots}: Beaufort {reading.BeaufortNumber}.",
            OccurredAt = now,
            IsSimulation = true
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await riskEngine.EvaluateRiskAsync(reading, cancellationToken);
    }

    private async Task CompleteSessionAsync(Guid sessionId, string targetStatus, string summary)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var session = await dbContext.SimulationSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId);
        if (session is null || session.Status != "RUNNING")
        {
            return;
        }

        session.Status = targetStatus;
        session.EndedAt = DateTimeOffset.UtcNow;
        dbContext.OperationEvents.Add(CreateSimulationEndedEvent(session, summary));
        await dbContext.SaveChangesAsync();
    }

    private static OperationEvent CreateSimulationEndedEvent(
        SimulationSession session,
        string summary)
        => new()
        {
            Id = Guid.NewGuid(),
            PortId = session.PortId,
            EventType = OperationEventType.SIMULATION_ENDED,
            ActorUserId = session.StartedByUserId,
            Payload = JsonSerializer.Serialize(new
            {
                sessionId = session.Id,
                session.ScenarioName,
                session.Status,
                session.TotalSnapshots
            }),
            Summary = summary,
            OccurredAt = DateTimeOffset.UtcNow,
            IsSimulation = true
        };

    private static void ValidateStartRequest(StartSimulationRequest request)
    {
        if (request.PortId == Guid.Empty)
        {
            throw new ArgumentException("PortId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ScenarioName))
        {
            throw new ArgumentException("ScenarioName is required.", nameof(request));
        }

        if (request.SpeedMultiplier is < 1 or > 100)
        {
            throw new ArgumentException("SpeedMultiplier must be between 1 and 100.", nameof(request));
        }

        if (request.WeatherSnapshots.Count < MinimumSnapshots)
        {
            throw new ArgumentException($"At least {MinimumSnapshots} weather snapshots are required.", nameof(request));
        }

        foreach (var snapshot in request.WeatherSnapshots)
        {
            if (snapshot.WindSpeedMs < 0 ||
                snapshot.Rainfall1hMm < 0 ||
                snapshot.VisibilityKm < 0 ||
                snapshot.HumidityPct is < 0 or > 100)
            {
                throw new ArgumentException(
                    "Simulation snapshots cannot contain negative wind, rain, visibility, or invalid humidity.",
                    nameof(request));
            }
        }
    }

    private static TimeSpan GetReplayDelay(short speedMultiplier)
        => TimeSpan.FromSeconds((double)BaseSnapshotIntervalSeconds / speedMultiplier);

    private static TimeSpan EstimateRemaining(int totalSnapshots, int completedSnapshots, short speedMultiplier)
    {
        var remainingSnapshots = Math.Max(0, totalSnapshots - completedSnapshots);
        return TimeSpan.FromSeconds(remainingSnapshots * (double)BaseSnapshotIntervalSeconds / speedMultiplier);
    }

    private static async Task<int> CountSimulationReadingsAsync(
        IApplicationDbContext dbContext,
        SimulationSession session,
        CancellationToken cancellationToken)
    {
        var to = session.EndedAt ?? DateTimeOffset.UtcNow;
        return await dbContext.WeatherReadings.CountAsync(x =>
            x.PortId == session.PortId &&
            x.IsSimulation &&
            x.RecordedAt >= session.StartedAt &&
            x.RecordedAt <= to,
            cancellationToken);
    }

    private static SimulationSessionDto ToDto(SimulationSession session)
        => new(
            session.Id,
            session.PortId,
            session.StartedByUserId,
            session.ScenarioName,
            session.SpeedMultiplier,
            session.TotalSnapshots,
            session.Status,
            session.StartedAt,
            session.EndedAt);

    private sealed class SimulationRuntimeState : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public SimulationRuntimeState(
            IReadOnlyList<SimulationWeatherSnapshotDto> snapshots,
            short speedMultiplier)
        {
            Snapshots = snapshots;
            SpeedMultiplier = speedMultiplier;
        }

        public IReadOnlyList<SimulationWeatherSnapshotDto> Snapshots { get; }
        public short SpeedMultiplier { get; }
        public int CompletedSnapshots { get; set; }
        public int? CurrentSnapshotNumber { get; set; }
        public SimulationWeatherSnapshotDto? CurrentWeather { get; set; }
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public Task CancelAsync()
        {
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
