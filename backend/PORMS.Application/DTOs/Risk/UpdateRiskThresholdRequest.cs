namespace PORMS.Application.DTOs.Risk;

public sealed record UpdateRiskThresholdRequest(
    decimal MinValue,
    decimal? MaxValue,
    string? Description,
    bool IsActive = true);
