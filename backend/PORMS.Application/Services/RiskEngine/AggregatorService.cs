using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public static class AggregatorService
{
    public static RiskLevel Aggregate(RiskLevel wind, RiskLevel rain, RiskLevel visibility)
        => new[] { wind, rain, visibility }.Max();

    public static RiskLevel Aggregate(RiskLevel wind, RiskLevel rain, RiskLevel? visibility)
        => Aggregate(wind, rain, visibility ?? RiskLevel.LOW);
}
