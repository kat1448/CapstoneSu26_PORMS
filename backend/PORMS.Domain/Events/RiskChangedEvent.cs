using PORMS.Domain.Enums;

namespace PORMS.Domain.Events;

public sealed class RiskChangedEvent
{
    public Guid PortId { get; init; }
    public Guid? ZoneId { get; init; }
    public RiskLevel? OldLevel { get; init; }
    public RiskLevel NewLevel { get; init; }
    public Guid RiskAssessmentId { get; init; }
    public DateTimeOffset AssessedAt { get; init; }
    public bool IsSimulation { get; init; }
}
