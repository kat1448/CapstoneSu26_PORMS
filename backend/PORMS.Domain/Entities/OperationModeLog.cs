using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class OperationModeLog
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public OperationMode? PreviousMode { get; set; }
    public OperationMode NewMode { get; set; }
    public RiskLevel? TriggeredByRiskLevel { get; set; }
    public Guid? TriggeredBySopRuleId { get; set; }
    public Guid? OverriddenByUserId { get; set; }
    public string? OverrideReason { get; set; }
    public string ChangeType { get; set; } = "AUTOMATIC";
    public DateTimeOffset ChangedAt { get; set; }
    public bool IsSimulation { get; set; }

    public Port? Port { get; set; }
    public SopRule? TriggeredBySopRule { get; set; }
}
