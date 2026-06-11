using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Alerts;

public sealed record AlertDto(
    Guid Id,
    Guid PortId,
    string AlertType,
    AlertSeverity Severity,
    string Title,
    string Message,
    string? Metadata,
    Guid? RelatedSopRuleId,
    Guid? RelatedAssessmentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    Guid? ReadByUserId,
    bool IsSimulation);
