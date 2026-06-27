namespace PORMS.API.Contracts;

public sealed class PortSummaryResponse
{
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public string CurrentRiskLevel { get; set; } = string.Empty;
    public string CurrentOperationMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long ActiveAlertCount { get; set; }
    public string UpdatedAtLabel { get; set; } = string.Empty;
}

public sealed record CreatePortRequest(
    string Code,
    string Name,
    string? Address,
    decimal Latitude,
    decimal Longitude,
    string Timezone,
    string WeatherSource,
    string? WeatherStationId,
    bool IsActive,
    IReadOnlyList<CreateZoneRequest> Zones);

public sealed record CreateZoneRequest(
    string Name,
    string ZoneType,
    decimal? CapacityValue,
    string? CapacityUnit,
    decimal? Latitude,
    decimal? Longitude,
    short DisplayOrder);
