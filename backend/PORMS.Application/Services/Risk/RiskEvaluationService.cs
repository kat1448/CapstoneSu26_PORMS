using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Events;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.Services.RiskEngine;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Domain.Events;

namespace PORMS.Application.Services.Risk;

public sealed class RiskEvaluationService : IRiskEvaluationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IThresholdLoader _thresholdLoader;
    private readonly IRiskAssessmentRepository _assessmentRepository;
    private readonly IDomainEventPublisher _eventPublisher;

    public RiskEvaluationService(
        IApplicationDbContext dbContext,
        IThresholdLoader thresholdLoader,
        IRiskAssessmentRepository assessmentRepository,
        IDomainEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _thresholdLoader = thresholdLoader;
        _assessmentRepository = assessmentRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<RiskAssessment> EvaluateAsync(
        Guid weatherReadingId,
        CancellationToken cancellationToken = default)
    {
        var weatherReading = await _dbContext.WeatherReadings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == weatherReadingId, cancellationToken);

        if (weatherReading is null)
        {
            throw new InvalidOperationException($"Weather reading {weatherReadingId} was not found.");
        }

        return await EvaluateAsync(weatherReading, cancellationToken);
    }

    public async Task<RiskAssessment> EvaluateAsync(
        WeatherReading weatherReading,
        CancellationToken cancellationToken = default)
    {
        var thresholds = await _thresholdLoader.GetThresholdsAsync(weatherReading.PortId, cancellationToken);

        var wind = WindEvaluator.EvaluateDetailed(
            weatherReading.WindSpeedMs,
            weatherReading.BeaufortNumber,
            thresholds);
        var rain = RainEvaluator.EvaluateDetailed(weatherReading.Rainfall1hMm, thresholds);
        var visibility = VisibilityEvaluator.EvaluateDetailed(weatherReading.VisibilityKm, thresholds);

        var finalRisk = AggregatorService.Aggregate(
            wind.RiskLevel,
            rain.RiskLevel,
            visibility.RiskLevel);

        var previousAssessment = await _assessmentRepository.GetLatestAsync(
            weatherReading.PortId,
            weatherReading.IsSimulation,
            cancellationToken);
        var previousRisk = previousAssessment?.FinalRiskLevel;
        var levelChanged = previousRisk != finalRisk;
        var evaluatedAt = DateTimeOffset.UtcNow;

        var assessment = new RiskAssessment
        {
            Id = Guid.NewGuid(),
            PortId = weatherReading.PortId,
            WeatherReadingId = weatherReading.Id,
            FinalRiskLevel = finalRisk,
            WindRiskLevel = wind.RiskLevel,
            RainRiskLevel = rain.RiskLevel,
            VisibilityRiskLevel = visibility.RiskLevel,
            BeaufortNumber = weatherReading.BeaufortNumber,
            Rainfall1hMm = weatherReading.Rainfall1hMm,
            VisibilityKm = weatherReading.VisibilityKm,
            PreviousRiskLevel = previousRisk,
            LevelChanged = levelChanged,
            AssessmentSummary = SummaryGenerator.Generate(weatherReading, wind, rain, visibility, finalRisk),
            EvaluatedAt = evaluatedAt,
            IsSimulation = weatherReading.IsSimulation
        };

        var details = new[]
        {
            ToDetail(assessment.Id, wind),
            ToDetail(assessment.Id, rain),
            ToDetail(assessment.Id, visibility)
        };

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
            OccurredAt = evaluatedAt,
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
                OccurredAt = evaluatedAt,
                IsSimulation = assessment.IsSimulation
            });
        }

        if (!assessment.IsSimulation)
        {
            var port = await _dbContext.Ports.FirstOrDefaultAsync(x => x.Id == assessment.PortId, cancellationToken);
            if (port is not null)
            {
                port.CurrentRiskLevel = assessment.FinalRiskLevel;
                port.UpdatedAt = evaluatedAt;
            }
        }

        await _assessmentRepository.SaveAsync(assessment, details, cancellationToken);

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

    private static RiskAssessmentDetail ToDetail(Guid assessmentId, FactorRiskEvaluation evaluation)
        => new()
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            Factor = evaluation.Factor,
            RawValue = evaluation.RawValue,
            BeaufortNumber = evaluation.BeaufortNumber,
            RiskLevel = evaluation.RiskLevel,
            Unit = evaluation.Unit,
            ThresholdApplied = evaluation.ThresholdApplied
        };
}
