using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Mode;

public sealed record OperationModeLogDto(
    Guid Id,
    Guid PortId,
    OperationMode? PreviousMode,
    OperationMode NewMode,
    RiskLevel? TriggeredByRiskLevel,
    Guid? TriggeredBySopRuleId,
    Guid? OverriddenByUserId,
    string? OverrideReason,
    string ChangeType,
    DateTimeOffset ChangedAt,
    bool IsSimulation);
