using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    [HttpGet("datasets")]
    public async Task<ActionResult<IReadOnlyList<SimulationDatasetSummaryResponse>>> GetDatasets(
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var datasets = await repository.GetDatasetsAsync(cancellationToken);
        return Ok(datasets.Select(ToResponse).ToList());
    }

    [HttpGet("map-points")]
    public async Task<ActionResult<IReadOnlyList<SimulationMapPointResponse>>> GetMapPoints(
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var points = await repository.GetMapPointsAsync(cancellationToken);
        return Ok(points.Select(ToResponse).ToList());
    }

    [HttpPost("datasets")]
    public async Task<ActionResult<SimulationDatasetSummaryResponse>> CreateDataset(
        [FromBody] CreateSimulationDatasetRequest request,
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        if (request.Snapshots.Count == 0)
        {
            return BadRequest(new ErrorResponse { Error = "At least one simulation snapshot is required." });
        }

        var created = await repository.CreateDatasetAsync(
            new CreateSimulationDatasetReadModel(
                request.Name,
                request.Description,
                request.PortCode,
                request.Snapshots.Select(item => new CreateSimulationSnapshotReadModel(
                    item.SnapshotNumber,
                    item.WindSpeedMs,
                    item.BeaufortNumber,
                    item.Rainfall1hMm,
                    item.VisibilityKm,
                    item.ZoneId)).ToList()),
            cancellationToken);

        return Created($"/api/simulation/datasets/{created.DatasetId}", ToResponse(created));
    }

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

    [HttpPost("run")]
    public async Task<ActionResult<object>> RunDataset(
        [FromBody] RunSimulationDatasetRequest request,
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.RunDatasetAsync(request.DatasetId, cancellationToken);
        if (result is null)
        {
            return NotFound(new ErrorResponse { Error = "The requested simulation dataset was not found." });
        }

        return Ok(new
        {
            sessionId = result.SessionId,
            finalRiskLevel = result.FinalRiskLevel,
            generatedTaskCount = result.GeneratedTaskCount
        });
    }

    [HttpGet("{sessionId:guid}/result")]
    public async Task<ActionResult<SimulationResultResponse>> GetResult(
        Guid sessionId,
        [FromServices] SimulationRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetResultAsync(sessionId, cancellationToken);
        if (result is null)
        {
            return NotFound(new ErrorResponse { Error = "The requested simulation result was not found." });
        }

        return Ok(new SimulationResultResponse
        {
            SessionId = result.SessionId,
            MapPoints = result.MapPoints.Select(ToResponse).ToList(),
            DangerousZones = result.DangerousZones.Select(item => new SimulationDangerousZoneResponse
            {
                ZoneId = item.ZoneId,
                ZoneName = item.ZoneName,
                RiskLevel = item.RiskLevel,
                Reason = item.Reason
            }).ToList(),
            Tasks = result.Tasks.Select(item => new SimulationGeneratedTaskResponse
            {
                TaskCode = item.TaskCode,
                Title = item.Title,
                Priority = item.Priority,
                ZoneName = item.ZoneName
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
            generatedTaskCount = result.GeneratedTaskCount,
            finalRiskLevel = result.FinalRiskLevel,
            finalOperationMode = result.FinalOperationMode
        });
    }

    private static SimulationDatasetSummaryResponse ToResponse(SimulationDatasetSummaryReadModel dataset)
    {
        return new SimulationDatasetSummaryResponse
        {
            DatasetId = dataset.DatasetId,
            Name = dataset.Name,
            Description = dataset.Description,
            PortCode = dataset.PortCode,
            SnapshotCount = dataset.SnapshotCount
        };
    }

    private static SimulationMapPointResponse ToResponse(SimulationMapPointReadModel point)
    {
        return new SimulationMapPointResponse
        {
            ZoneId = point.ZoneId,
            ZoneName = point.ZoneName,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            RiskLevel = point.RiskLevel
        };
    }
}
