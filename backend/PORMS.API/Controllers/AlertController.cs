using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AllAppUsers")]
[Route("api/alerts")]
public sealed class AlertController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> GetAlerts(
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();

        var alerts = await repository.GetAlertsAsync(
            access.UserId.Value, access.Role, access.PortId, cancellationToken);
        return Ok(alerts.Select(ToResponse).ToList());
    }

    [HttpGet("{alertId:guid}")]
    public async Task<ActionResult<AlertResponse>> GetAlert(
        Guid alertId,
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var alert = await FindAccessibleAlertAsync(alertId, repository, cancellationToken);
        return alert is null ? NotFound() : Ok(ToResponse(alert));
    }

    [HttpGet("{alertId:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<TaskLogResponse>>> GetAlertTasks(
        Guid alertId,
        [FromServices] AlertRepository alertRepository,
        [FromServices] TaskRepository taskRepository,
        CancellationToken cancellationToken)
    {
        var alert = await FindAccessibleAlertAsync(alertId, alertRepository, cancellationToken);
        if (alert is null) return NotFound();

        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var tasks = await taskRepository.GetTasksByAlertAsync(
            alertId, access.UserId.Value, access.Role, cancellationToken);
        return Ok(tasks.Select(TaskController.ToResponse).ToList());
    }

    [HttpGet("{alertId:guid}/speech")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> GetAlertSpeech(
        Guid alertId,
        [FromServices] AlertRepository repository,
        [FromServices] GoogleTranslateSpeechService speechService,
        CancellationToken cancellationToken)
    {
        var alert = await FindAccessibleAlertAsync(alertId, repository, cancellationToken);
        if (alert is null || alert.Severity is not ("HIGH" or "CRITICAL")) return NotFound();

        var audio = await speechService.SynthesizeAlertAsync(alert, cancellationToken);
        return File(audio, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpPatch("{alertId:guid}/acknowledge")]
    public async Task<ActionResult<AlertResponse>> AcknowledgeAlert(
        Guid alertId,
        [FromServices] AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();

        var acknowledged = await repository.AcknowledgeAlertAsync(
            alertId, access.UserId.Value, cancellationToken);
        if (!acknowledged) return NotFound();

        var alert = await FindAccessibleAlertAsync(alertId, repository, cancellationToken);
        return alert is null ? NotFound() : Ok(ToResponse(alert));
    }

    private async Task<AlertReadModel?> FindAccessibleAlertAsync(
        Guid alertId,
        AlertRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return null;

        var alerts = await repository.GetAlertsAsync(
            access.UserId.Value, access.Role, access.PortId, cancellationToken);
        return alerts.SingleOrDefault(item => item.AlertId == alertId);
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
            BeaufortNumber = alert.BeaufortNumber,
            WindSpeedMs = alert.WindSpeedMs,
            Rainfall1hMm = alert.Rainfall1hMm,
            VisibilityKm = alert.VisibilityKm,
            RecipientCount = alert.RecipientCount,
            ReadCount = alert.ReadCount,
            AcknowledgedCount = alert.AcknowledgedCount,
            Read = alert.Read,
            Acknowledged = alert.Acknowledged,
            AcknowledgedAt = alert.AcknowledgedAt,
            Status = alert.Acknowledged ? "ACKNOWLEDGED" : alert.Read ? "READ" : "NEW"
        };

    private static (Guid? UserId, string Role, Guid? PortId) GetAccessScope(ClaimsPrincipal user)
    {
        var rawUserId = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var rawPortId = user.FindFirstValue("port_id");

        return (
            Guid.TryParse(rawUserId, out var userId) ? userId : null,
            role,
            Guid.TryParse(rawPortId, out var portId) ? portId : null);
    }
}
