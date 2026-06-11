using System.Text.Json;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Services.Risk;
using PORMS.Application.Services.Weather;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Infrastructure.Weather;
using PORMS.Infrastructure.Data;

namespace PORMS.API.BackgroundServices;

public sealed class WeatherUpdateWorker : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeatherUpdateWorker> _logger;
    private readonly TimeSpan _interval;

    public WeatherUpdateWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeatherUpdateWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue("WeatherUpdate:IntervalMinutes", 15));

        if (_interval <= TimeSpan.Zero)
        {
            _interval = DefaultInterval;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Weather update worker started. Interval={Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Weather update worker cycle failed unexpectedly.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        List<Port> activePorts;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            activePorts = await dbContext.Ports
                .AsNoTracking()
                .Where(port => port.IsActive)
                .OrderBy(port => port.Code)
                .ToListAsync(cancellationToken);
        }

        if (activePorts.Count == 0)
        {
            _logger.LogInformation("No active ports found for weather update.");
            return;
        }

        _logger.LogInformation("Fetching OpenWeather data for {PortCount} active ports.", activePorts.Count);

        foreach (var port in activePorts)
        {
            await ProcessPortAsync(port, cancellationToken);
        }
    }

    private async Task ProcessPortAsync(
        Port port,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
        var riskEngine = scope.ServiceProvider.GetRequiredService<IRiskEngine>();
        var stopwatch = Stopwatch.StartNew();
        var fetchJob = new WeatherFetchJob
        {
            Id = Guid.NewGuid(),
            PortId = port.Id,
            Status = "PENDING",
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            dbContext.WeatherFetchJobs.Add(fetchJob);
            await dbContext.SaveChangesAsync(cancellationToken);

            var reading = await weatherService.FetchCurrentWeatherAsync(port, cancellationToken);
            dbContext.WeatherReadings.Add(reading);
            fetchJob.Status = "SUCCESS";
            fetchJob.CompletedAt = DateTimeOffset.UtcNow;
            fetchJob.ResponseTimeMs = GetElapsedMilliseconds(stopwatch);
            fetchJob.HttpStatusCode = StatusCodes.Status200OK;
            fetchJob.CreatedReadingId = reading.Id;
            dbContext.OperationEvents.Add(new OperationEvent
            {
                Id = Guid.NewGuid(),
                PortId = port.Id,
                EventType = OperationEventType.WEATHER_FETCHED,
                Payload = JsonSerializer.Serialize(new
                {
                    readingId = reading.Id,
                    windSpeedMs = reading.WindSpeedMs,
                    reading.BeaufortNumber,
                    reading.Rainfall1hMm,
                    reading.VisibilityKm,
                    observedAt = reading.ObservedAt
                }),
                Summary = $"Weather fetched for {port.Code}: wind {reading.WindSpeedMs:0.0} m/s, Beaufort {reading.BeaufortNumber}.",
                OccurredAt = DateTimeOffset.UtcNow,
                IsSimulation = false
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await riskEngine.EvaluateRiskAsync(reading, cancellationToken);

            _logger.LogInformation(
                "Weather update succeeded for port {PortCode} ({PortId}). ReadingId={ReadingId}",
                port.Code,
                port.Id,
                reading.Id);
        }
        catch (OpenWeatherException exception)
        {
            await MarkFetchJobFailedAsync(
                dbContext,
                fetchJob.Id,
                exception.StatusCode.HasValue ? (int)exception.StatusCode.Value : null,
                exception.Message,
                stopwatch,
                cancellationToken);
            _logger.LogWarning(
                exception,
                "OpenWeather fetch failed for port {PortCode} ({PortId}). StatusCode={StatusCode}. The worker will continue with the next port.",
                port.Code,
                port.Id,
                exception.StatusCode);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception)
        {
            await MarkFetchJobFailedAsync(
                dbContext,
                fetchJob.Id,
                null,
                exception.GetBaseException().Message,
                stopwatch,
                cancellationToken);
            _logger.LogError(
                exception,
                "Database save failed while processing weather for port {PortCode} ({PortId}).",
                port.Code,
                port.Id);
            dbContext.ChangeTracker.Clear();
        }
        catch (Exception exception)
        {
            await MarkFetchJobFailedAsync(
                dbContext,
                fetchJob.Id,
                null,
                exception.Message,
                stopwatch,
                cancellationToken);
            _logger.LogError(
                exception,
                "Weather update failed for port {PortCode} ({PortId}). The worker will continue with the next port.",
                port.Code,
                port.Id);
            dbContext.ChangeTracker.Clear();
        }
    }

    private static async Task MarkFetchJobFailedAsync(
        ApplicationDbContext dbContext,
        Guid fetchJobId,
        int? httpStatusCode,
        string errorMessage,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var fetchJob = await dbContext.WeatherFetchJobs
            .FirstOrDefaultAsync(x => x.Id == fetchJobId, cancellationToken);

        if (fetchJob is null)
        {
            return;
        }

        fetchJob.Status = "FAILED";
        fetchJob.CompletedAt = DateTimeOffset.UtcNow;
        fetchJob.ResponseTimeMs = GetElapsedMilliseconds(stopwatch);
        fetchJob.HttpStatusCode = httpStatusCode;
        fetchJob.ErrorMessage = Truncate(errorMessage, 1000);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int GetElapsedMilliseconds(Stopwatch stopwatch)
        => (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
