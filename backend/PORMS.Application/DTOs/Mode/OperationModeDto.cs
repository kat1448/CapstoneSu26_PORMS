using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Mode;

public sealed record OperationModeDto(
    Guid PortId,
    OperationMode CurrentMode,
    RiskLevel CurrentRiskLevel,
    DateTimeOffset? LastChangedAt,
    string? LastChangeType,
    Guid? LastChangedByRuleId);
