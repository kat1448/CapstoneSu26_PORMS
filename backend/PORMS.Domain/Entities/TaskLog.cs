using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class TaskLog
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid TriggeredByRuleId { get; set; }
    public Guid? TriggeredByAssessmentId { get; set; }
    public SopActionType ActionType { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public RiskLevel RiskLevelAtCreation { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsSimulation { get; set; }

    public Port? Port { get; set; }
    public Zone? Zone { get; set; }
    public SopRule? TriggeredByRule { get; set; }
}
