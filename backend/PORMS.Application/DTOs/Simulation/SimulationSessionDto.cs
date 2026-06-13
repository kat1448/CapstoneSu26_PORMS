namespace PORMS.Application.DTOs.Simulation;

public sealed record SimulationSessionDto(
    Guid Id,
    Guid PortId,
    Guid StartedByUserId,
    string ScenarioName,
    short SpeedMultiplier,
    int TotalSnapshots,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
