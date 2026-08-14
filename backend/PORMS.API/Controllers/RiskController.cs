using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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
    [Authorize(Policy = "AdminOnly")]
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

    /// Tải template Excel chứa cấu hình ngưỡng rủi ro hiện tại
    [HttpGet("thresholds/import-template")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DownloadThresholdImportTemplate(
        [FromServices] RiskThresholdImportService importService,
        CancellationToken cancellationToken)
    {
        var content = await importService.CreateTemplateAsync(
            cancellationToken);

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "PORMS_RiskThresholds_Template.xlsx");
    }

    /// Phân tích file Excel và trả về kết quả xem trước
    /// Endpoint này tuyệt đối không ghi dữ liệu vào database
    [HttpPost("thresholds/import/preview")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    public async Task<ActionResult<RiskThresholdImportPreviewResponse>>
        PreviewThresholdImport(
            [FromServices] RiskThresholdImportService importService,
            [FromForm] RiskThresholdImportRequest request,
            CancellationToken cancellationToken)
    {
        var preview = await importService.PreviewAsync(
            request.File,
            cancellationToken);

        // File không hợp lệ vẫn trả 200 để frontend hiển thị chi tiết lỗi
        return Ok(preview);
    }

    /// Kiểm tra lại file và lưu toàn bộ thay đổi trong một transaction
    [HttpPost("thresholds/import")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    public async Task<ActionResult<RiskThresholdImportResponse>>
        ImportThresholds(
            [FromServices] RiskThresholdImportService importService,
            [FromForm] RiskThresholdImportRequest request,
            CancellationToken cancellationToken)
    {
        var actorUserId = GetUserId(User);

        if (actorUserId is null)
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "Không xác định được người dùng từ access token."
            });
        }

        var result = await importService.ImportAsync(
            request.File,
            request.ChangeReason,
            actorUserId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            // Không có dữ liệu nào được ghi nếu file hoặc request còn lỗi
            return BadRequest(result.Preview);
        }

        return Ok(new RiskThresholdImportResponse
        {
            FileName = result.Preview.FileName,
            CreatedCount = result.Preview.CreateCount,
            UpdatedCount = result.Preview.UpdateCount,
            UnchangedCount = result.Preview.UnchangedCount,
            Configuration = ToResponse(result.Configuration!)
        });
    }

    [HttpPut("zones/{zoneId:guid}/threshold-overrides")]
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteZoneThresholdOverride(
        [FromServices] RiskRepository repository,
        Guid zoneId,
        Guid overrideId,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteZoneThresholdOverrideAsync(zoneId, overrideId, cancellationToken);
        return deleted ? NoContent() : NotFound(new ErrorResponse { Error = "Zone threshold override was not found." });
    }

    /// Đọc user ID từ JWT, hỗ trợ cả claim gốc và claim đã được ASP.NET ánh xạ
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var rawUserId =
            user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(rawUserId, out var userId)
            ? userId
            : null;
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
