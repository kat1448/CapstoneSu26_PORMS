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
            ActiveAlertCount = port.ActiveAlertCount,
            UpdatedAtLabel = FormatUpdatedAtLabel(port.LastWeatherFetchAt)
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
            IsActive = zone.IsActive,
            CapacityLabel = FormatCapacityLabel(zone.CapacityValue, zone.CapacityUnit),
            StatusLabel = GetZoneStatusLabel(zone.CurrentRiskLevel, zone.IsRestricted),
            OverrideEnabled = zone.OverrideEnabled,
            DisplayOrder = zone.DisplayOrder
        }).ToList());
    }

    private static string FormatUpdatedAtLabel(DateTimeOffset? updatedAt)
    {
        if (updatedAt is null)
        {
            return "Chưa có dữ liệu";
        }

        var elapsed = DateTimeOffset.UtcNow - updatedAt.Value.ToUniversalTime();
        if (elapsed.TotalMinutes < 1)
        {
            return "Vừa cập nhật";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)} phút trước";
        }

        return $"{Math.Floor(elapsed.TotalHours)} giờ trước";
    }

    private static string FormatCapacityLabel(decimal? capacityValue, string? capacityUnit)
    {
        if (capacityValue is null || string.IsNullOrWhiteSpace(capacityUnit))
        {
            return "Chưa cấu hình";
        }

        return $"{capacityValue:0.##} {capacityUnit}";
    }

    private static string GetZoneStatusLabel(string riskLevel, bool isRestricted)
    {
        if (isRestricted)
        {
            return riskLevel is "CRITICAL" ? "Tạm dừng" : "Hạn chế";
        }

        return riskLevel switch
        {
            "LOW" => "Bình thường",
            "MEDIUM" => "Tăng giám sát",
            "HIGH" => "Hạn chế",
            "CRITICAL" => "Tạm dừng",
            _ => "Không xác định"
        };
    }
}
