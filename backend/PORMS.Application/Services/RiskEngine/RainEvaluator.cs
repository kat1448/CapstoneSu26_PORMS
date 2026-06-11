using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public static class RainEvaluator
{
    public static RiskLevel Evaluate(double? rainfall1hMm, IEnumerable<RiskThreshold> thresholds)
        => EvaluateDetailed(rainfall1hMm.HasValue ? (decimal?)rainfall1hMm.Value : null, thresholds).RiskLevel;

    public static FactorRiskEvaluation EvaluateDetailed(
        decimal? rainfall1hMm,
        IEnumerable<RiskThreshold> thresholds)
    {
        if (rainfall1hMm is < 0)
        {
            throw new ArgumentException("Rainfall cannot be negative.", nameof(rainfall1hMm));
        }

        if (!rainfall1hMm.HasValue || rainfall1hMm.Value == 0)
        {
            return new FactorRiskEvaluation(
                WeatherFactor.RAIN,
                rainfall1hMm ?? 0,
                null,
                RiskLevel.LOW,
                "mm/h",
                0,
                10);
        }

        return RiskThresholdMatcher.MatchAscending(
            WeatherFactor.RAIN,
            rainfall1hMm.Value,
            null,
            "mm/h",
            thresholds);
    }
}
