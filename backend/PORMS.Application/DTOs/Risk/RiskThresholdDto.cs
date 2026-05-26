using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskThresholdDto(
    Guid Id,
    Guid? PortId,
    WeatherFactor Factor,
    RiskLevel RiskLevel,
    decimal MinValue,
    decimal? MaxValue,
    string Unit,
    string? Description,
    bool IsActive);
