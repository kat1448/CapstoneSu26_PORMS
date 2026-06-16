using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Simulation;

public sealed record SimulationResultsDto(
    Guid SessionId,
    Guid PortId,
    string ScenarioName,
    string Status,
    int TotalSnapshots,
    int WeatherReadings,
    int RiskAssessments,
    int RiskChanges,
    int SopExecutions,
    int AlertsGenerated,
    int TasksGenerated,
    RiskLevel? PeakRiskLevel,
    IReadOnlyDictionary<string, int> RiskDistribution,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
