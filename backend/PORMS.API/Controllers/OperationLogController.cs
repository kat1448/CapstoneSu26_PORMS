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
        [FromServices] OperationEventRepository repository,
        CancellationToken cancellationToken)
    {
        var events = await repository.GetOperationEventsAsync(cancellationToken);

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
            OccurredAt = operationEvent.OccurredAt
        }).ToList());
    }
}
