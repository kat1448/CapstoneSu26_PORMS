using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetAlerts(
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var alerts = await repository.GetAlertsAsync(cancellationToken);

        return Ok(alerts.Select(alert => new AlertResponse
        {
            AlertId = alert.AlertId,
            PortId = alert.PortId,
            PortCode = alert.PortCode,
            PortName = alert.PortName,
            ZoneId = alert.ZoneId,
            ZoneName = alert.ZoneName,
            AlertType = alert.AlertType,
            Severity = alert.Severity,
            Title = alert.Title,
            Message = alert.Message,
            CreatedAt = alert.CreatedAt,
            ExpiresAt = alert.ExpiresAt,
            RecipientCount = alert.RecipientCount,
            ReadCount = alert.ReadCount,
            AcknowledgedCount = alert.AcknowledgedCount,
            Read = alert.RecipientCount > 0 && alert.ReadCount >= alert.RecipientCount
        }).ToList());
    }

    [HttpGet("{alertId:guid}")]
    public async Task<ActionResult<AlertResponse>> GetAlert(
        Guid alertId,
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var alerts = await repository.GetAlertsAsync(cancellationToken);
        var alert = alerts.SingleOrDefault(item => item.AlertId == alertId);
        return alert is null
            ? NotFound()
            : Ok(ToResponse(alert));
    }

    [HttpGet("{alertId:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<TaskLogResponse>>> GetAlertTasks(
        Guid alertId,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.GetTasksByAlertAsync(alertId, cancellationToken);
        return Ok(tasks.Select(TaskController.ToResponse).ToList());
    }

    [HttpPatch("{alertId:guid}/acknowledge")]
    [Authorize(Policy = "AllAppUsers")]
    public async Task<ActionResult<AlertResponse>> AcknowledgeAlert(
        Guid alertId,
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var acknowledged = await repository.AcknowledgeAlertAsync(alertId, userId.Value, cancellationToken);
        if (!acknowledged)
        {
            return NotFound();
        }

        var alerts = await repository.GetAlertsAsync(cancellationToken);
        var alert = alerts.SingleOrDefault(item => item.AlertId == alertId);
        return alert is null ? NotFound() : Ok(ToResponse(alert));
    }

    private static AlertResponse ToResponse(AlertReadModel alert) =>
        new()
        {
            AlertId = alert.AlertId,
            PortId = alert.PortId,
            PortCode = alert.PortCode,
            PortName = alert.PortName,
            ZoneId = alert.ZoneId,
            ZoneName = alert.ZoneName,
            AlertType = alert.AlertType,
            Severity = alert.Severity,
            Title = alert.Title,
            Message = alert.Message,
            CreatedAt = alert.CreatedAt,
            ExpiresAt = alert.ExpiresAt,
            RecipientCount = alert.RecipientCount,
            ReadCount = alert.ReadCount,
            AcknowledgedCount = alert.AcknowledgedCount,
            Read = alert.RecipientCount > 0 && alert.ReadCount >= alert.RecipientCount
        };

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var rawUserId = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
