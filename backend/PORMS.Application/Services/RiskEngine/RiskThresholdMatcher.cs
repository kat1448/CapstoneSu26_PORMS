using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

internal static class RiskThresholdMatcher
{
    public static FactorRiskEvaluation MatchAscending(
        WeatherFactor factor,
        decimal value,
        int? beaufortNumber,
        string fallbackUnit,
        IEnumerable<RiskThreshold> thresholds)
    {
        var activeThresholds = thresholds
            .Where(x => x.IsActive && x.Factor == factor)
            .OrderBy(x => x.MinValue)
            .ToList();

        return Match(factor, value, beaufortNumber, fallbackUnit, activeThresholds);
    }

    public static FactorRiskEvaluation MatchDescending(
        WeatherFactor factor,
        decimal value,
        int? beaufortNumber,
        string fallbackUnit,
        IEnumerable<RiskThreshold> thresholds)
    {
        var activeThresholds = thresholds
            .Where(x => x.IsActive && x.Factor == factor)
            .OrderByDescending(x => x.MinValue)
            .ToList();

        return Match(factor, value, beaufortNumber, fallbackUnit, activeThresholds);
    }

    private static FactorRiskEvaluation Match(
        WeatherFactor factor,
        decimal value,
        int? beaufortNumber,
        string fallbackUnit,
        IReadOnlyList<RiskThreshold> thresholds)
    {
        if (thresholds.Count == 0)
        {
            throw new InvalidOperationException($"No active thresholds configured for factor {factor}.");
        }

        var threshold = thresholds.FirstOrDefault(x =>
            value >= x.MinValue &&
            (!x.MaxValue.HasValue || value < x.MaxValue.Value));

        threshold ??= value < thresholds.Min(x => x.MinValue)
            ? thresholds.OrderBy(x => x.MinValue).First()
            : thresholds.OrderByDescending(x => x.MinValue).First();

        return new FactorRiskEvaluation(
            factor,
            value,
            beaufortNumber,
            threshold.RiskLevel,
            string.IsNullOrWhiteSpace(threshold.Unit) ? fallbackUnit : threshold.Unit,
            threshold.MinValue,
            threshold.MaxValue);
    }
}
