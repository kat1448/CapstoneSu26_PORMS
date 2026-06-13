using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class SopRule
{
    public Guid Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public RiskLevel TriggerRiskLevel { get; set; }
    public ZoneType? AppliesToZoneType { get; set; }
    public SopActionType ActionType { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public OperationMode? TargetOperationMode { get; set; }
    public short ExecutionOrder { get; set; } = 100;
    public string? AlertMessage { get; set; }
    public AlertSeverity AlertSeverity { get; set; } = AlertSeverity.WARNING;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
