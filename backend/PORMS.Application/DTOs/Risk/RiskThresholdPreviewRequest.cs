namespace PORMS.Application.DTOs.Risk;

public sealed record RiskThresholdPreviewRequest(
    decimal WindSpeedMs,
    decimal? Rainfall1hMm,
    decimal? VisibilityKm,
    IReadOnlyList<RiskThresholdDraftDto> Drafts);
