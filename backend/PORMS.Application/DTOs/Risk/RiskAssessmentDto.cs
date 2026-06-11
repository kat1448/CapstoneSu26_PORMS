using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskAssessmentDto(
    Guid Id,
    Guid PortId,
    Guid WeatherReadingId,
    RiskLevel FinalRiskLevel,
    RiskLevel WindRiskLevel,
    RiskLevel RainRiskLevel,
    RiskLevel? VisibilityRiskLevel,
    RiskLevel? PreviousRiskLevel,
    bool LevelChanged,
    int BeaufortNumber,
    decimal? Rainfall1hMm,
    decimal? VisibilityKm,
    string? AssessmentSummary,
    DateTimeOffset EvaluatedAt,
    bool IsSimulation);
