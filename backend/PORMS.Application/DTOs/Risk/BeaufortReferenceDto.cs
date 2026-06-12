using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record BeaufortReferenceDto(
    int Number,
    string Name,
    decimal MinWindSpeedMs,
    decimal? MaxWindSpeedMs,
    RiskLevel RiskLevel);
