using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/ports")]
public sealed class PortController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortSummaryResponse>>> GetPorts(
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var ports = await repository.GetPortsAsync(cancellationToken);

        return Ok(ports.Select(port => new PortSummaryResponse
        {
            PortId = port.PortId,
            PortCode = port.PortCode,
            PortName = port.PortName,
            CurrentRiskLevel = port.CurrentRiskLevel,
            CurrentOperationMode = port.CurrentOperationMode,
            IsActive = port.IsActive,
            ActiveAlertCount = port.ActiveAlertCount
        }).ToList());
    }

    [HttpGet("{portId:guid}/zones")]
    public async Task<ActionResult<IReadOnlyList<ZoneResponse>>> GetZones(
        Guid portId,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var zones = await repository.GetZonesAsync(portId, cancellationToken);

        return Ok(zones.Select(zone => new ZoneResponse
        {
            ZoneId = zone.ZoneId,
            PortId = zone.PortId,
            ZoneName = zone.ZoneName,
            ZoneType = zone.ZoneType,
            CurrentRiskLevel = zone.CurrentRiskLevel,
            IsRestricted = zone.IsRestricted,
            RestrictionReason = zone.RestrictionReason,
            IsActive = zone.IsActive
        }).ToList());
    }
}
