namespace PORMS.API.Contracts;

public sealed class OperationEventResponse
{
    public Guid OperationEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? PortId { get; set; }
    public string? PortCode { get; set; }
    public string? PortName { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? SimulationSessionId { get; set; }
    public bool IsSimulation { get; set; }
    public string Tone { get; set; } = "info";
}
