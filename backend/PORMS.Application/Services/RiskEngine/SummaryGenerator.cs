using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.RiskEngine;

public static class SummaryGenerator
{
    public static string Generate(
        WeatherReading reading,
        FactorRiskEvaluation wind,
        FactorRiskEvaluation rain,
        FactorRiskEvaluation visibility,
        RiskLevel finalRisk)
    {
        var rainText = reading.Rainfall1hMm.GetValueOrDefault() == 0
            ? "Rain: none"
            : $"Rain: {reading.Rainfall1hMm:0.0} mm/h => {ToVietnameseLabel(rain.RiskLevel)}";

        var visibilityText = reading.VisibilityKm.HasValue
            ? $"Visibility: {reading.VisibilityKm:0.0} km => {ToVietnameseLabel(visibility.RiskLevel)}"
            : "Visibility: no data => LOW";

        return $"Wind: Beaufort {reading.BeaufortNumber} ({reading.WindSpeedMs:0.0} m/s) => {ToVietnameseLabel(wind.RiskLevel)}; " +
               $"{rainText}; {visibilityText} | Final: {ToVietnameseLabel(finalRisk)}";
    }

    private static string ToVietnameseLabel(RiskLevel riskLevel) => riskLevel switch
    {
        RiskLevel.LOW => "LOW",
        RiskLevel.MEDIUM => "MEDIUM",
        RiskLevel.HIGH => "HIGH",
        RiskLevel.CRITICAL => "CRITICAL",
        _ => riskLevel.ToString()
    };
}
