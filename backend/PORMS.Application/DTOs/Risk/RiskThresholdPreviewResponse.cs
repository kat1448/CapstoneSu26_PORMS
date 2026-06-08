using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskThresholdPreviewResponse(
    RiskLevel CurrentResult,
    RiskLevel PreviewResult,
    bool WouldChange);
