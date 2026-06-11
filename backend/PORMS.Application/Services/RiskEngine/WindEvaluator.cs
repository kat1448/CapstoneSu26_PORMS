using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public static class WindEvaluator
{
    public static RiskLevel Evaluate(double windSpeedMs, IEnumerable<RiskThreshold> thresholds)
        => EvaluateDetailed((decimal)windSpeedMs, null, thresholds).RiskLevel;

    public static FactorRiskEvaluation EvaluateDetailed(
        decimal windSpeedMs,
        int? beaufortNumber,
        IEnumerable<RiskThreshold> thresholds)
    {
        if (windSpeedMs < 0)
        {
            throw new ArgumentException("Wind speed cannot be negative.", nameof(windSpeedMs));
        }

        return RiskThresholdMatcher.MatchAscending(
            WeatherFactor.WIND,
            windSpeedMs,
            beaufortNumber,
            "m/s",
            thresholds);
    }
}
