namespace PORMS.Application.DTOs.Simulation;

public sealed record SimulationStatusDto(
    Guid SessionId,
    Guid PortId,
    string ScenarioName,
    string Status,
    short SpeedMultiplier,
    int TotalSnapshots,
    int CompletedSnapshots,
    decimal PercentComplete,
    int? CurrentSnapshotNumber,
    SimulationWeatherSnapshotDto? CurrentWeather,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    TimeSpan? EstimatedRemaining);
