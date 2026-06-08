using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Risk;
using PORMS.Application.Services.RiskEngine;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Risk;

public sealed class RiskThresholdService : IRiskThresholdService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IThresholdLoader _thresholdLoader;

    public RiskThresholdService(IApplicationDbContext dbContext, IThresholdLoader thresholdLoader)
    {
        _dbContext = dbContext;
        _thresholdLoader = thresholdLoader;
    }

    public async Task<IReadOnlyList<RiskThreshold>> GetGlobalThresholdsAsync(
        CancellationToken cancellationToken = default)
        => await _dbContext.RiskThresholds
            .AsNoTracking()
            .OrderBy(x => x.Factor)
            .ThenBy(x => x.MinValue)
            .ToListAsync(cancellationToken);

    public async Task<RiskThreshold> UpdateAsync(
        Guid id,
        UpdateRiskThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MinValue < 0 || request.MaxValue <= request.MinValue)
        {
            throw new ArgumentException("Threshold values must satisfy min >= 0 and max > min when max is provided.");
        }

        var threshold = await _dbContext.RiskThresholds
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (threshold is null)
        {
            throw new KeyNotFoundException($"Risk threshold {id} was not found.");
        }

        var oldMin = threshold.MinValue;
        var oldMax = threshold.MaxValue;
        var oldDescription = threshold.Description;

        threshold.MinValue = request.MinValue;
        threshold.MaxValue = request.MaxValue;
        threshold.Description = request.Description ?? threshold.Description;
        threshold.IsActive = request.IsActive;
        threshold.UpdatedAt = DateTimeOffset.UtcNow;

        await ValidateNoGapOrOverlapAsync(threshold.Factor, cancellationToken);

        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = null,
            EventType = OperationEventType.THRESHOLD_UPDATED,
            Payload = JsonSerializer.Serialize(new
            {
                factor = threshold.Factor.ToString(),
                riskLevel = threshold.RiskLevel.ToString(),
                oldMin,
                oldMax,
                oldDescription,
                newMin = threshold.MinValue,
                newMax = threshold.MaxValue,
                newDescription = threshold.Description
            }),
            Summary = $"Risk threshold updated: {threshold.Factor}/{threshold.RiskLevel}.",
            OccurredAt = threshold.UpdatedAt,
            IsSimulation = false
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        _thresholdLoader.InvalidateCache();

        return threshold;
    }

    public async Task<RiskThresholdPreviewResponse> PreviewAsync(
        RiskThresholdPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentThresholds = await _thresholdLoader.GetThresholdsAsync(Guid.Empty, cancellationToken);
        var previewThresholds = currentThresholds
            .Select(CloneThreshold)
            .ToList();

        foreach (var draft in request.Drafts)
        {
            var existing = previewThresholds.FirstOrDefault(x =>
                x.Factor == draft.Factor &&
                x.RiskLevel == draft.RiskLevel);

            if (existing is null)
            {
                previewThresholds.Add(new RiskThreshold
                {
                    Id = Guid.Empty,
                    Factor = draft.Factor,
                    RiskLevel = draft.RiskLevel,
                    MinValue = draft.MinValue,
                    MaxValue = draft.MaxValue,
                    Unit = draft.Unit ?? GetDefaultUnit(draft.Factor),
                    IsActive = true
                });
            }
            else
            {
                existing.MinValue = draft.MinValue;
                existing.MaxValue = draft.MaxValue;
                existing.Unit = draft.Unit ?? existing.Unit;
            }
        }

        var currentResult = Evaluate(request, currentThresholds);
        var previewResult = Evaluate(request, previewThresholds);

        return new RiskThresholdPreviewResponse(
            currentResult,
            previewResult,
            currentResult != previewResult);
    }

    private async Task ValidateNoGapOrOverlapAsync(
        WeatherFactor factor,
        CancellationToken cancellationToken)
    {
        var thresholds = await _dbContext.RiskThresholds
            .Where(x => x.Factor == factor && x.IsActive)
            .OrderBy(x => x.MinValue)
            .ToListAsync(cancellationToken);

        if (thresholds.Count == 0)
        {
            return;
        }

        if (thresholds.Count(x => x.MaxValue is null) != 1)
        {
            throw new InvalidOperationException($"Factor {factor} must have exactly one open-ended threshold.");
        }

        for (var i = 0; i < thresholds.Count - 1; i++)
        {
            var current = thresholds[i];
            var next = thresholds[i + 1];

            if (!current.MaxValue.HasValue)
            {
                throw new InvalidOperationException($"Only the highest threshold for {factor} can have no max value.");
            }

            if (current.MaxValue.Value != next.MinValue)
            {
                throw new InvalidOperationException($"Thresholds for {factor} must not contain gaps or overlaps.");
            }
        }
    }

    private static RiskLevel Evaluate(
        RiskThresholdPreviewRequest request,
        IEnumerable<RiskThreshold> thresholds)
    {
        var wind = WindEvaluator.Evaluate((double)request.WindSpeedMs, thresholds);
        var rain = RainEvaluator.Evaluate(request.Rainfall1hMm.HasValue ? (double?)request.Rainfall1hMm.Value : null, thresholds);
        var visibility = VisibilityEvaluator.Evaluate(request.VisibilityKm.HasValue ? (double?)request.VisibilityKm.Value : null, thresholds);

        return AggregatorService.Aggregate(wind, rain, visibility);
    }

    private static RiskThreshold CloneThreshold(RiskThreshold threshold)
        => new()
        {
            Id = threshold.Id,
            Factor = threshold.Factor,
            RiskLevel = threshold.RiskLevel,
            MinValue = threshold.MinValue,
            MaxValue = threshold.MaxValue,
            Unit = threshold.Unit,
            Description = threshold.Description,
            IsActive = threshold.IsActive,
            CreatedAt = threshold.CreatedAt,
            UpdatedAt = threshold.UpdatedAt,
            UpdatedByUserId = threshold.UpdatedByUserId
        };

    private static string GetDefaultUnit(WeatherFactor factor) => factor switch
    {
        WeatherFactor.WIND => "m/s",
        WeatherFactor.RAIN => "mm/h",
        WeatherFactor.VISIBILITY => "km",
        _ => string.Empty
    };
}
