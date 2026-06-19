using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromServices] DashboardRepository repository,
        CancellationToken cancellationToken)
    {
        var summary = await repository.GetSummaryAsync(cancellationToken);

        if (summary is null)
        {
            return NotFound(new { error = "No dashboard data available." });
        }

        return Ok(new DashboardSummaryResponse
        {
            PortId = summary.PortId,
            PortCode = summary.PortCode,
            PortName = summary.PortName,
            CurrentRiskLevel = summary.CurrentRiskLevel,
            CurrentOperationMode = summary.CurrentOperationMode,
            WindSpeedMs = summary.WindSpeedMs,
            BeaufortNumber = summary.BeaufortNumber,
            Rainfall1hMm = summary.Rainfall1hMm,
            VisibilityKm = summary.VisibilityKm,
            ActiveAlertCount = summary.ActiveAlertCount
        });
    }
}
