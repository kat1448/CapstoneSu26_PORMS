using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class OperationEvent
{
    public Guid Id { get; set; }
    public Guid? PortId { get; set; }
    public OperationEventType EventType { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Payload { get; set; } = "{}";
    public string? Summary { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public bool IsSimulation { get; set; }
}
