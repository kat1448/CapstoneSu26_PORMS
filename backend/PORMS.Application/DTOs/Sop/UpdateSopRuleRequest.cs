using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Sop;

public sealed record UpdateSopRuleRequest(
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
    Guid? UpdatedByUserId,
    string ChangeReason);
