using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskAssessmentDto(
    Guid Id,
    Guid PortId,
    Guid WeatherReadingId,
    RiskLevel FinalRiskLevel,
    RiskLevel? PreviousRiskLevel,
    bool LevelChanged,
    string AssessmentSummary,
    DateTimeOffset AssessedAt,
    bool IsSimulation,
    IReadOnlyList<RiskAssessmentDetailDto> Details);
