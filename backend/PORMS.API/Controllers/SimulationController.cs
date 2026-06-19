using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<SimulationSnapshotResponse>> GetCurrent(
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var snapshot = await repository.GetCurrentAsync(cancellationToken);

        return Ok(new SimulationSnapshotResponse
        {
            Status = snapshot.Status,
            CurrentRiskLevel = snapshot.CurrentRiskLevel,
            CurrentMode = snapshot.CurrentMode,
            WindSpeedMs = snapshot.WindSpeedMs,
            BeaufortNumber = snapshot.BeaufortNumber,
            Rainfall1hMm = snapshot.Rainfall1hMm,
            VisibilityKm = snapshot.VisibilityKm,
            ProgressPercent = snapshot.ProgressPercent,
            GeneratedAlertCount = snapshot.GeneratedAlertCount,
            ModeChangeCount = snapshot.ModeChangeCount,
            Feed = snapshot.Feed.Select(item => new SimulationFeedItemResponse
            {
                Title = item.Title,
                Detail = item.Detail,
                RiskLevel = item.RiskLevel,
                HappenedAt = item.HappenedAt
            }).ToList()
        });
    }

    [HttpPost("run-demo")]
    public async Task<ActionResult<object>> RunDemo(
        [FromBody] SimulationRunRequest? request,
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.RunDemoAsync(request?.PortCode, cancellationToken);
        if (result is null)
        {
            return NotFound(new ErrorResponse
            {
                Error = "The requested port was not found."
            });
        }

        return Ok(new
        {
            sessionId = result.SessionId,
            portId = result.PortId,
            portCode = result.PortCode,
            stepCount = result.StepCount,
            generatedAlertCount = result.GeneratedAlertCount,
            modeChangeCount = result.ModeChangeCount,
            finalRiskLevel = result.FinalRiskLevel,
            finalOperationMode = result.FinalOperationMode
        });
    }
}
