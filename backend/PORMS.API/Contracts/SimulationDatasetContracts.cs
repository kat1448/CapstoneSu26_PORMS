namespace PORMS.API.Contracts;

public sealed class SimulationDatasetSummaryResponse
{
    public required Guid DatasetId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string PortCode { get; init; }
    public required int SnapshotCount { get; init; }
}

public sealed class SimulationDatasetDetailResponse
{
    public required Guid DatasetId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string PortCode { get; init; }
    public required int SnapshotCount { get; init; }
    public required IReadOnlyList<SimulationDatasetSnapshotResponse> Snapshots { get; init; }
}

public sealed class SimulationDatasetSnapshotResponse
{
    public required int SnapshotNumber { get; init; }
    public required decimal WindSpeedMs { get; init; }
    public required short BeaufortNumber { get; init; }
    public required decimal Rainfall1hMm { get; init; }
    public decimal? VisibilityKm { get; init; }
    public Guid? ZoneId { get; init; }
}

public sealed class CreateSimulationDatasetRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string PortCode { get; init; }
    public required IReadOnlyList<CreateSimulationSnapshotRequest> Snapshots { get; init; }
}

public sealed class CreateSimulationSnapshotRequest
{
    public required int SnapshotNumber { get; init; }
    public required decimal WindSpeedMs { get; init; }
    public required short BeaufortNumber { get; init; }
    public required decimal Rainfall1hMm { get; init; }
    public decimal? VisibilityKm { get; init; }
    public Guid? ZoneId { get; init; }
}

public sealed class RunSimulationDatasetRequest
{
    public required Guid DatasetId { get; init; }
}

public sealed class SimulationResultResponse
{
    public required Guid SessionId { get; init; }
    public required IReadOnlyList<SimulationMapPointResponse> MapPoints { get; init; }
    public required IReadOnlyList<SimulationDangerousZoneResponse> DangerousZones { get; init; }
    public required IReadOnlyList<SimulationGeneratedTaskResponse> Tasks { get; init; }
}

public sealed class SimulationMapPointResponse
{
    public required Guid ZoneId { get; init; }
    public required string ZoneName { get; init; }
    public required decimal Latitude { get; init; }
    public required decimal Longitude { get; init; }
    public required string RiskLevel { get; init; }
}

public sealed class SimulationDangerousZoneResponse
{
    public required Guid ZoneId { get; init; }
    public required string ZoneName { get; init; }
    public required string RiskLevel { get; init; }
    public string? Reason { get; init; }
}

public sealed class SimulationGeneratedTaskResponse
{
    public required string TaskCode { get; init; }
    public required string Title { get; init; }
    public required string Priority { get; init; }
    public string? ZoneName { get; init; }
}
