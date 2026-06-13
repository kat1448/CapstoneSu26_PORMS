using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Sop;

public sealed record SopRecommendationDto(
    Guid RuleId,
    string RuleName,
    Guid? ZoneId,
    string? ZoneName,
    ZoneType? ZoneType,
    RiskLevel TriggerRiskLevel,
    SopActionType ActionType,
    string ActionDescription,
    OperationMode? TargetOperationMode,
    short ExecutionOrder,
    AlertSeverity AlertSeverity,
    string? AlertMessage);
