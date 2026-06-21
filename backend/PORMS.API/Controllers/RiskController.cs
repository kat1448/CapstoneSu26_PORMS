using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskController : ControllerBase
{
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<RiskTrendPointResponse>>> GetTrend(
        [FromServices] RiskRepository repository,
        CancellationToken cancellationToken)
    {
        var trend = await repository.GetTrendAsync(cancellationToken);

        return Ok(trend.Select(point => new RiskTrendPointResponse
        {
            HourLabel = point.HourLabel,
            RiskScore = point.RiskScore
        }).ToList());
    }
}
