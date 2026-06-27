namespace PORMS.API.Contracts;

public sealed class SimulationRunRequest
{
    public string? PortCode { get; set; }
    public Guid? DatasetId { get; set; }
}
