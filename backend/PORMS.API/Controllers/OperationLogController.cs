using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/operation-events")]
public sealed class OperationLogController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperationEventResponse>>> GetOperationEvents(
        [FromQuery] string? scope,
        [FromServices] OperationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var simulationOnly = string.Equals(scope, "simulation", StringComparison.OrdinalIgnoreCase);
        var events = await repository.GetOperationEventsAsync(simulationOnly, cancellationToken);

        return Ok(events.Select(operationEvent => new OperationEventResponse
        {
            OperationEventId = operationEvent.OperationEventId,
            EventType = operationEvent.EventType,
            PortId = operationEvent.PortId,
            PortCode = operationEvent.PortCode,
            PortName = operationEvent.PortName,
            ZoneId = operationEvent.ZoneId,
            ZoneName = operationEvent.ZoneName,
            ActorUserId = operationEvent.ActorUserId,
            ActorName = operationEvent.ActorName,
            EntityType = operationEvent.EntityType,
            EntityId = operationEvent.EntityId,
            Summary = operationEvent.Summary,
            OccurredAt = operationEvent.OccurredAt,
            SimulationSessionId = operationEvent.SimulationSessionId,
            IsSimulation = operationEvent.SimulationSessionId.HasValue,
            Tone = GetTone(operationEvent.EventType)
        }).ToList());
    }

    private static string GetTone(string eventType)
    {
        return eventType switch
        {
            "MODE_CHANGED" => "danger",
            "RISK_CHANGED" => "warning",
            "SIMULATION_STEP" => "warning",
            "SIMULATION_COMPLETED" => "success",
            "USER_LOGIN" => "success",
            _ => "info"
        };
    }
}
