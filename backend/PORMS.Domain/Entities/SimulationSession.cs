namespace PORMS.Domain.Entities;

public class SimulationSession
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public Port? Port { get; set; }
    public Guid StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public string ScenarioName { get; set; } = string.Empty;
    public short SpeedMultiplier { get; set; } = 10;
    public int TotalSnapshots { get; set; }
    public string Status { get; set; } = "RUNNING";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}
