using Microsoft.AspNetCore.Authorization;
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

    [HttpGet("thresholds")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<ActionResult<RiskConfigResponse>> GetThresholds(
        [FromServices] RiskRepository repository,
        CancellationToken cancellationToken)
    {
        var config = await repository.GetConfigAsync(cancellationToken);
        return Ok(ToResponse(config));
    }

    [HttpPut("thresholds")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<ActionResult<RiskConfigResponse>> SaveThresholds(
        [FromServices] RiskRepository repository,
        [FromBody] SaveRiskThresholdsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Thresholds.Count == 0)
        {
            return BadRequest(new ErrorResponse { Error = "At least one threshold is required." });
        }

        await repository.SaveThresholdsAsync(
            request.Thresholds.Select(item => new SaveRiskThresholdReadModel(
                item.Factor,
                item.RiskLevel,
                item.ComparisonOperator,
                item.ThresholdValue,
                item.Unit,
                item.Description,
                item.Version,
                item.IsEnabled)).ToList(),
            request.ChangeReason,
            cancellationToken);

        return Ok(ToResponse(await repository.GetConfigAsync(cancellationToken)));
    }

    [HttpPut("zones/{zoneId:guid}/threshold-overrides")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<ActionResult<RiskConfigResponse>> SaveZoneThresholdOverrides(
        [FromServices] RiskRepository repository,
        Guid zoneId,
        [FromBody] SaveZoneThresholdOverridesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Overrides.Count == 0)
        {
            return BadRequest(new ErrorResponse { Error = "At least one zone threshold override is required." });
        }

        await repository.SaveZoneThresholdOverridesAsync(
            zoneId,
            request.Overrides.Select(item => new SaveZoneThresholdOverrideReadModel(
                item.Factor,
                item.RiskLevel,
                item.ComparisonOperator,
                item.ThresholdValue,
                item.Unit,
                item.IsEnabled)).ToList(),
            request.ChangeReason,
            cancellationToken);

        return Ok(ToResponse(await repository.GetConfigAsync(cancellationToken)));
    }

    [HttpDelete("zones/{zoneId:guid}/threshold-overrides/{overrideId:guid}")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<IActionResult> DeleteZoneThresholdOverride(
        [FromServices] RiskRepository repository,
        Guid zoneId,
        Guid overrideId,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteZoneThresholdOverrideAsync(zoneId, overrideId, cancellationToken);
        return deleted ? NoContent() : NotFound(new ErrorResponse { Error = "Zone threshold override was not found." });
    }

    private static RiskConfigResponse ToResponse(RiskConfigReadModel config)
    {
        return new RiskConfigResponse
        {
            Thresholds = config.Thresholds.Select(item => new RiskThresholdResponse
            {
                Id = item.Id,
                Factor = item.Factor,
                RiskLevel = item.RiskLevel,
                ComparisonOperator = ToUiOperator(item.ComparisonOperator),
                ThresholdValue = item.ThresholdValue,
                Unit = item.Unit,
                Description = item.Description,
                Version = item.Version,
                IsEnabled = item.IsEnabled,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            ZoneOverrides = config.ZoneOverrides.Select(item => new ZoneThresholdOverrideResponse
            {
                Id = item.Id,
                ZoneId = item.ZoneId,
                ZoneName = item.ZoneName,
                ZoneType = item.ZoneType,
                Factor = item.Factor,
                RiskLevel = item.RiskLevel,
                ComparisonOperator = ToUiOperator(item.ComparisonOperator),
                ThresholdValue = item.ThresholdValue,
                Unit = item.Unit,
                IsEnabled = item.IsEnabled,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            Zones = config.Zones.Select(item => new RiskConfigZoneResponse
            {
                ZoneId = item.ZoneId,
                ZoneName = item.ZoneName,
                ZoneType = item.ZoneType,
                PortName = item.PortName
            }).ToList()
        };
    }

    private static string ToUiOperator(string value)
    {
        return value switch
        {
            "GTE" => ">=",
            "LTE" => "<=",
            _ => value
        };
    }
}
