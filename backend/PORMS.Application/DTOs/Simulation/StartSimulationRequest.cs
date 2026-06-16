namespace PORMS.Application.DTOs.Simulation;

public sealed record StartSimulationRequest(
    Guid PortId,
    string ScenarioName,
    IReadOnlyList<SimulationWeatherSnapshotDto> WeatherSnapshots,
    short SpeedMultiplier = 10,
    Guid? StartedByUserId = null);
