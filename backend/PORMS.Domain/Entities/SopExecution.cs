using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class SopExecution
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public Guid? RiskAssessmentId { get; set; }
    public Guid PortId { get; set; }
    public Guid? ZoneId { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public string ExecutionResult { get; set; } = "{}";
    public string? SkipReason { get; set; }
    public int DurationMs { get; set; }
    public bool IsSimulation { get; set; }

    public SopRule? Rule { get; set; }
    public RiskAssessment? RiskAssessment { get; set; }
    public Port? Port { get; set; }
    public Zone? Zone { get; set; }
}
