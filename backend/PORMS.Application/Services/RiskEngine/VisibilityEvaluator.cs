using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public static class VisibilityEvaluator
{
    public static RiskLevel Evaluate(double? visibilityKm, IEnumerable<RiskThreshold> thresholds)
        => EvaluateDetailed(visibilityKm.HasValue ? (decimal?)visibilityKm.Value : null, thresholds).RiskLevel;

    public static FactorRiskEvaluation EvaluateDetailed(
        decimal? visibilityKm,
        IEnumerable<RiskThreshold> thresholds)
    {
        if (visibilityKm is < 0)
        {
            throw new ArgumentException("Visibility cannot be negative.", nameof(visibilityKm));
        }

        if (!visibilityKm.HasValue)
        {
            return new FactorRiskEvaluation(
                WeatherFactor.VISIBILITY,
                0,
                null,
                RiskLevel.LOW,
                "km",
                10,
                null);
        }

        return RiskThresholdMatcher.MatchDescending(
            WeatherFactor.VISIBILITY,
            visibilityKm.Value,
            null,
            "km",
            thresholds);
    }
}
