using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskThresholdDraftDto(
    WeatherFactor Factor,
    RiskLevel RiskLevel,
    decimal MinValue,
    decimal? MaxValue,
    string? Unit = null);
