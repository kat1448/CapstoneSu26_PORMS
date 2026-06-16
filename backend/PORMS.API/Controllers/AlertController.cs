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
            AcknowledgedCount = alert.AcknowledgedCount
        }).ToList());
    }
}
