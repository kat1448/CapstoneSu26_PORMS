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
    public decimal? CapacityValue { get; set; }
    public string? CapacityUnit { get; set; }
    public string CapacityLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public bool OverrideEnabled { get; set; }
    public short DisplayOrder { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed record UpdateZoneRequest(
    string Name,
    string ZoneType,
    decimal? CapacityValue,
    string? CapacityUnit,
    decimal? Latitude,
    decimal? Longitude,
    short DisplayOrder,
    bool IsActive);
