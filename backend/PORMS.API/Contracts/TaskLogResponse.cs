namespace PORMS.API.Contracts;

public sealed class TaskLogResponse
{
    public Guid TaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public string? AssignedTeam { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public Guid? SimulationSessionId { get; set; }
    public bool IsSimulation { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
