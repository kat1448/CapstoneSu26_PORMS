namespace PORMS.API.Contracts;

public sealed class ZoneResponse
{
    public Guid ZoneId { get; set; }
    public Guid PortId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string ZoneType { get; set; } = string.Empty;
    public string CurrentRiskLevel { get; set; } = string.Empty;
    public bool IsRestricted { get; set; }
    public string? RestrictionReason { get; set; }
    public bool IsActive { get; set; }
}
