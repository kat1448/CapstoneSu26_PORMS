using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AllAppUsers")]
[Route("api/operation-events")]
public sealed class OperationLogController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperationEventResponse>>> GetOperationEvents(
        [FromQuery] string? scope,
        [FromServices] OperationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawUserId, out var userId)) return Unauthorized();

        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var simulationOnly = string.Equals(scope, "simulation", StringComparison.OrdinalIgnoreCase);
        var events = await repository.GetOperationEventsAsync(simulationOnly, userId, role, cancellationToken);

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
            SimulationDatasetName = operationEvent.SimulationDatasetName,
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
            "TASK_ASSIGNED" => "info",
            "TASK_ACKNOWLEDGED" => "info",
            "TASK_STARTED" => "warning",
            "TASK_COMPLETED" => "success",
            "USER_LOGIN" => "success",
            _ => "info"
        };
    }
}
