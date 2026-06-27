using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/ports")]
public sealed class PortController : ControllerBase
{
    private static readonly HashSet<string> AllowedZoneTypes = ["DOCK", "YARD", "GATE", "WAREHOUSE"];

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortSummaryResponse>>> GetPorts(
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var ports = await repository.GetPortsAsync(cancellationToken);
        return Ok(ports.Select(ToResponse).ToList());
    }

    [HttpGet("{portId:guid}")]
    public async Task<ActionResult<PortSummaryResponse>> GetPort(
        Guid portId,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var port = await repository.GetPortAsync(portId, cancellationToken);
        return port is null ? NotFound() : Ok(ToResponse(port));
    }

    [HttpPost]
    public async Task<ActionResult<PortSummaryResponse>> CreatePort(
        CreatePortRequest request,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreatePort(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var created = await repository.CreatePortAsync(
            new CreatePortReadModel(
                request.Code,
                request.Name,
                request.Address,
                request.Latitude,
                request.Longitude,
                request.Timezone,
                request.WeatherSource,
                request.WeatherStationId,
                request.IsActive,
                request.Zones.Select(zone => new CreateZoneReadModel(
                    zone.Name,
                    zone.ZoneType,
                    zone.CapacityValue,
                    zone.CapacityUnit,
                    zone.Latitude,
                    zone.Longitude,
                    zone.DisplayOrder)).ToList()),
            cancellationToken);

        return CreatedAtAction(nameof(GetPorts), new { portId = created.PortId }, ToResponse(created));
    }

    [HttpPut("{portId:guid}")]
    public async Task<ActionResult<PortSummaryResponse>> UpdatePort(
        Guid portId,
        UpdatePortRequest request,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateUpdatePort(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var updated = await repository.UpdatePortAsync(
            portId,
            new UpdatePortReadModel(
                request.Code,
                request.Name,
                request.Address,
                request.Latitude,
                request.Longitude,
                request.Timezone,
                request.WeatherSource,
                request.WeatherStationId,
                request.IsActive),
            cancellationToken);

        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpGet("{portId:guid}/zones")]
    public async Task<ActionResult<IReadOnlyList<ZoneResponse>>> GetZones(
        Guid portId,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var zones = await repository.GetZonesAsync(portId, cancellationToken);
        return Ok(zones.Select(ToResponse).ToList());
    }

    [HttpPut("{portId:guid}/zones/{zoneId:guid}")]
    public async Task<ActionResult<ZoneResponse>> UpdateZone(
        Guid portId,
        Guid zoneId,
        UpdateZoneRequest request,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateZone(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var updated = await repository.UpdateZoneAsync(
            portId,
            zoneId,
            new UpdateZoneReadModel(
                request.Name,
                request.ZoneType,
                request.CapacityValue,
                request.CapacityUnit,
                request.Latitude,
                request.Longitude,
                request.DisplayOrder,
                request.IsActive),
            cancellationToken);

        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpDelete("{portId:guid}/zones/{zoneId:guid}")]
    public async Task<IActionResult> DeleteZone(
        Guid portId,
        Guid zoneId,
        [FromServices] PortRepository repository,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteZoneAsync(portId, zoneId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static PortSummaryResponse ToResponse(PortSummaryReadModel port) =>
        new()
        {
            PortId = port.PortId,
            PortCode = port.PortCode,
            PortName = port.PortName,
            Latitude = port.Latitude,
            Longitude = port.Longitude,
            CurrentRiskLevel = port.CurrentRiskLevel,
            CurrentOperationMode = port.CurrentOperationMode,
            IsActive = port.IsActive,
            ActiveAlertCount = port.ActiveAlertCount,
            UpdatedAtLabel = FormatUpdatedAtLabel(port.LastWeatherFetchAt)
        };

    private static ZoneResponse ToResponse(ZoneReadModel zone) =>
        new()
        {
            ZoneId = zone.ZoneId,
            PortId = zone.PortId,
            ZoneName = zone.ZoneName,
            ZoneType = zone.ZoneType,
            CurrentRiskLevel = zone.CurrentRiskLevel,
            IsRestricted = zone.IsRestricted,
            RestrictionReason = zone.RestrictionReason,
            IsActive = zone.IsActive,
            CapacityValue = zone.CapacityValue,
            CapacityUnit = zone.CapacityUnit,
            CapacityLabel = FormatCapacityLabel(zone.CapacityValue, zone.CapacityUnit),
            StatusLabel = GetZoneStatusLabel(zone.CurrentRiskLevel, zone.IsRestricted),
            OverrideEnabled = zone.OverrideEnabled,
            DisplayOrder = zone.DisplayOrder,
            Latitude = zone.Latitude,
            Longitude = zone.Longitude
        };

    private static string? ValidateCreatePort(CreatePortRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return "Mã cảng và tên cảng là bắt buộc.";
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return "Tọa độ cảng không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(request.Timezone) || string.IsNullOrWhiteSpace(request.WeatherSource))
        {
            return "Timezone và nguồn thời tiết là bắt buộc.";
        }

        foreach (var zone in request.Zones)
        {
            var zoneError = ValidateZone(zone);
            if (zoneError is not null)
            {
                return zoneError;
            }
        }

        return null;
    }

    private static string? ValidateUpdatePort(UpdatePortRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return "Mã cảng và tên cảng là bắt buộc.";
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return "Tọa độ cảng không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(request.Timezone) || string.IsNullOrWhiteSpace(request.WeatherSource))
        {
            return "Timezone và nguồn thời tiết là bắt buộc.";
        }

        return null;
    }

    private static string? ValidateZone(CreateZoneRequest zone)
    {
        if (string.IsNullOrWhiteSpace(zone.Name))
        {
            return "Tên khu vực là bắt buộc.";
        }

        if (!AllowedZoneTypes.Contains(zone.ZoneType))
        {
            return "Loại khu vực không hợp lệ.";
        }

        if (zone.Latitude is < -90 or > 90 || zone.Longitude is < -180 or > 180)
        {
            return "Tọa độ khu vực không hợp lệ.";
        }

        return null;
    }

    private static string? ValidateZone(UpdateZoneRequest zone)
    {
        if (string.IsNullOrWhiteSpace(zone.Name))
        {
            return "Tên khu vực là bắt buộc.";
        }

        if (!AllowedZoneTypes.Contains(zone.ZoneType))
        {
            return "Loại khu vực không hợp lệ.";
        }

        if (zone.Latitude is < -90 or > 90 || zone.Longitude is < -180 or > 180)
        {
            return "Tọa độ khu vực không hợp lệ.";
        }

        return null;
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
