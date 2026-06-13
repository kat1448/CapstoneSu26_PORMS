using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Sop;

public sealed record SopRuleDto(
    Guid Id,
    string RuleName,
    RiskLevel TriggerRiskLevel,
    ZoneType? AppliesToZoneType,
    SopActionType ActionType,
    string ActionDescription,
    OperationMode? TargetOperationMode,
    short ExecutionOrder,
    string? AlertMessage,
    AlertSeverity AlertSeverity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int TotalExecutions = 0,
    DateTimeOffset? LastTriggeredAt = null);
