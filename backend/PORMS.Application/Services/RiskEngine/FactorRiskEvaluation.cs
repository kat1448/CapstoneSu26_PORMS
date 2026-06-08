using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public sealed record FactorRiskEvaluation(
    WeatherFactor Factor,
    decimal RawValue,
    int? BeaufortNumber,
    RiskLevel RiskLevel,
    string Unit,
    decimal MinValue,
    decimal? MaxValue)
{
    public string ThresholdApplied =>
        MaxValue.HasValue
            ? $"{MinValue:0.###} <= {Unit} < {MaxValue:0.###}"
            : $"{Unit} >= {MinValue:0.###}";
}
