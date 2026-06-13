using PORMS.Application.DTOs.Simulation;

namespace PORMS.Application.Services.Simulation;

public interface ISimulationService
{
    Task<SimulationSessionDto> StartAsync(
        StartSimulationRequest request,
        CancellationToken cancellationToken = default);

    Task<SimulationSessionDto> StopAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SimulationStatusDto?> GetStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SimulationResultsDto?> GetResultsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
