using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Tasks;

public sealed record TaskLogDto(
    Guid Id,
    Guid PortId,
    Guid? ZoneId,
    Guid TriggeredByRuleId,
    Guid? TriggeredByAssessmentId,
    SopActionType ActionType,
    string ActionDescription,
    RiskLevel RiskLevelAtCreation,
    DateTimeOffset CreatedAt,
    bool IsSimulation);
