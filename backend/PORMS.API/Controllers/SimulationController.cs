using Microsoft.AspNetCore.Mvc;
using PORMS.Application.DTOs.Simulation;
using PORMS.Application.Services.Simulation;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;

    public SimulationController(ISimulationService simulationService)
    {
        _simulationService = simulationService;
    }

    [HttpPost("start")]
    [ProducesResponseType<SimulationSessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationSessionDto>> StartAsync(
        [FromBody] StartSimulationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _simulationService.StartAsync(request, cancellationToken);
            return Created($"/api/simulation/status?sessionId={session.Id}", session);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("stop")]
    [ProducesResponseType<SimulationSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationSessionDto>> StopAsync(
        [FromBody] StopSimulationRequest? request,
        [FromQuery] Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var id = request?.SessionId ?? sessionId;
        if (id is null || id == Guid.Empty)
        {
            return BadRequest("sessionId is required in body or query string.");
        }

        try
        {
            return Ok(await _simulationService.StopAsync(id.Value, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("status")]
    [ProducesResponseType<SimulationStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationStatusDto>> GetStatusAsync(
        [FromQuery] Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            return BadRequest("sessionId is required.");
        }

        var status = await _simulationService.GetStatusAsync(sessionId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("{sessionId:guid}/results")]
    [ProducesResponseType<SimulationResultsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SimulationResultsDto>> GetResultsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var results = await _simulationService.GetResultsAsync(sessionId, cancellationToken);
        return results is null ? NotFound() : Ok(results);
    }

    [HttpGet("results/{sessionId:guid}")]
    [ProducesResponseType<SimulationResultsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<SimulationResultsDto>> GetResultsAliasAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => GetResultsAsync(sessionId, cancellationToken);
}

public sealed record StopSimulationRequest(Guid SessionId);
