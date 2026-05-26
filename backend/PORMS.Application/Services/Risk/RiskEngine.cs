using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Events;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Domain.Events;

namespace PORMS.Application.Services.Risk;

public sealed class RiskEngine : IRiskEngine
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDomainEventPublisher _eventPublisher;

    public RiskEngine(IApplicationDbContext dbContext, IDomainEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    public async Task<RiskAssessment> EvaluateRiskAsync(
        WeatherReading weatherReading,
        CancellationToken cancellationToken = default)
    {
        var previousRisk = await _dbContext.RiskAssessments
            .Where(x => x.PortId == weatherReading.PortId && !x.IsSimulation)
            .OrderByDescending(x => x.EvaluatedAt)
            .Select(x => (RiskLevel?)x.FinalRiskLevel)
            .FirstOrDefaultAsync(cancellationToken);

        var windRisk = BeaufortToRisk(weatherReading.BeaufortNumber);
        var rainRisk = RainToRisk(weatherReading.Rainfall1hMm ?? 0);
        var visibilityRisk = weatherReading.VisibilityKm.HasValue
            ? VisibilityToRisk(weatherReading.VisibilityKm.Value)
            : RiskLevel.LOW;

        var finalRisk = MaxRisk(windRisk, rainRisk, visibilityRisk);
        var levelChanged = previousRisk != finalRisk;

        var assessment = new RiskAssessment
        {
            Id = Guid.NewGuid(),
            PortId = weatherReading.PortId,
            WeatherReadingId = weatherReading.Id,
            FinalRiskLevel = finalRisk,
            WindRiskLevel = windRisk,
            RainRiskLevel = rainRisk,
            VisibilityRiskLevel = visibilityRisk,
            BeaufortNumber = weatherReading.BeaufortNumber,
            Rainfall1hMm = weatherReading.Rainfall1hMm,
            VisibilityKm = weatherReading.VisibilityKm,
            PreviousRiskLevel = previousRisk,
            LevelChanged = levelChanged,
            AssessmentSummary = BuildSummary(weatherReading, windRisk, rainRisk, visibilityRisk, finalRisk),
            EvaluatedAt = DateTimeOffset.UtcNow,
            IsSimulation = weatherReading.IsSimulation
        };

        _dbContext.RiskAssessments.Add(assessment);

        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = assessment.PortId,
            EventType = OperationEventType.RISK_ASSESSED,
            Payload = JsonSerializer.Serialize(new
            {
                assessmentId = assessment.Id,
                weatherReadingId = assessment.WeatherReadingId,
                finalRiskLevel = assessment.FinalRiskLevel.ToString(),
                windRiskLevel = assessment.WindRiskLevel.ToString(),
                rainRiskLevel = assessment.RainRiskLevel.ToString(),
                visibilityRiskLevel = assessment.VisibilityRiskLevel?.ToString(),
                assessment.BeaufortNumber,
                assessment.Rainfall1hMm,
                assessment.VisibilityKm
            }),
            Summary = assessment.AssessmentSummary,
            OccurredAt = assessment.EvaluatedAt,
            IsSimulation = assessment.IsSimulation
        });

        if (levelChanged)
        {
            _dbContext.OperationEvents.Add(new OperationEvent
            {
                Id = Guid.NewGuid(),
                PortId = assessment.PortId,
                EventType = OperationEventType.RISK_LEVEL_CHANGED,
                Payload = JsonSerializer.Serialize(new
                {
                    assessmentId = assessment.Id,
                    oldLevel = previousRisk?.ToString(),
                    newLevel = finalRisk.ToString(),
                    assessment.BeaufortNumber,
                    assessment.Rainfall1hMm,
                    assessment.VisibilityKm
                }),
                Summary = $"Risk level changed from {previousRisk?.ToString() ?? "NONE"} to {finalRisk}.",
                OccurredAt = assessment.EvaluatedAt,
                IsSimulation = assessment.IsSimulation
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (levelChanged)
        {
            await _eventPublisher.PublishAsync(new RiskChangedEvent
            {
                PortId = assessment.PortId,
                OldLevel = previousRisk,
                NewLevel = finalRisk,
                RiskAssessmentId = assessment.Id,
                AssessedAt = assessment.EvaluatedAt,
                IsSimulation = assessment.IsSimulation
            }, cancellationToken);
        }

        return assessment;
    }

    private static RiskLevel BeaufortToRisk(int beaufortNumber) => beaufortNumber switch
    {
        <= 5 => RiskLevel.LOW,
        <= 7 => RiskLevel.MEDIUM,
        <= 9 => RiskLevel.HIGH,
        _ => RiskLevel.CRITICAL
    };

    private static RiskLevel RainToRisk(decimal rainfall1hMm) => rainfall1hMm switch
    {
        < 10m => RiskLevel.LOW,
        < 25m => RiskLevel.MEDIUM,
        < 50m => RiskLevel.HIGH,
        _ => RiskLevel.CRITICAL
    };

    private static RiskLevel VisibilityToRisk(decimal visibilityKm) => visibilityKm switch
    {
        < 1m => RiskLevel.CRITICAL,
        < 5m => RiskLevel.HIGH,
        < 10m => RiskLevel.MEDIUM,
        _ => RiskLevel.LOW
    };

    private static RiskLevel MaxRisk(params RiskLevel[] levels)
        => levels.Max();

    private static string BuildSummary(
        WeatherReading reading,
        RiskLevel windRisk,
        RiskLevel rainRisk,
        RiskLevel visibilityRisk,
        RiskLevel finalRisk)
    {
        return $"Wind Beaufort {reading.BeaufortNumber} ({reading.WindSpeedMs:0.0} m/s) => {windRisk}; " +
               $"rain {(reading.Rainfall1hMm ?? 0):0.0} mm/h => {rainRisk}; " +
               $"visibility {(reading.VisibilityKm?.ToString("0.0") ?? "N/A")} km => {visibilityRisk}. " +
               $"Final risk = {finalRisk}.";
    }
}
