using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public Guid? RelatedSopRuleId { get; set; }
    public Guid? RelatedAssessmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public Guid? ReadByUserId { get; set; }
    public bool IsSimulation { get; set; }

    public Port? Port { get; set; }
    public SopRule? RelatedSopRule { get; set; }
}
