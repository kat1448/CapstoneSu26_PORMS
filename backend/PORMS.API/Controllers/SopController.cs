using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/sop-rules")]
public sealed class SopController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SopRulesResponse>> GetRules(
        [FromServices] SopRuleRepository repository,
        CancellationToken cancellationToken)
    {
        var rules = await repository.GetRulesAsync(cancellationToken);
        return Ok(ToResponse(rules));
    }

    /// Tải template Excel chứa các SOP hiện tại
    [HttpGet("import-template")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DownloadImportTemplate(
        [FromServices] SopRuleImportService importService,
        CancellationToken cancellationToken)
    {
        var content =
            await importService.CreateTemplateAsync(cancellationToken);

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "PORMS_SopRules_Template.xlsx");
    }

    /// Kiểm tra file và trả kết quả preview
    /// Endpoint này không ghi hoặc thay đổi database
    [HttpPost("import/preview")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    public async Task<ActionResult<SopRuleImportPreviewResponse>>
        PreviewImport(
            [FromServices] SopRuleImportService importService,
            [FromForm] SopRuleImportRequest request,
            CancellationToken cancellationToken)
    {
        var preview = await importService.PreviewAsync(
            request.File,
            cancellationToken);

        // File sai vẫn trả 200 để frontend có thể hiển thị từng lỗi
        return Ok(preview);
    }

    /// Kiểm tra lại file và lưu tất cả thay đổi trong một transaction
    [HttpPost("import")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    public async Task<ActionResult<SopRuleImportResponse>>
        ImportRules(
            [FromServices] SopRuleImportService importService,
            [FromForm] SopRuleImportRequest request,
            CancellationToken cancellationToken)
    {
        var actorUserId = GetUserId(User);

        if (!actorUserId.HasValue)
        {
            return Unauthorized(new ErrorResponse
            {
                Error =
                    "Không xác định được người dùng từ access token."
            });
        }

        var result = await importService.ImportAsync(
            request.File,
            request.ChangeReason,
            actorUserId.Value,
            cancellationToken);

        if (!result.IsSuccess)
        {
            // Không có dữ liệu nào được ghi nếu file còn lỗi
            return BadRequest(result.Preview);
        }

        return Ok(new SopRuleImportResponse
        {
            ImportBatchId = result.ImportBatchId!.Value,
            FileName = result.Preview.FileName,
            CreatedCount = result.Preview.CreateCount,
            UpdatedCount = result.Preview.UpdateCount,
            UnchangedCount = result.Preview.UnchangedCount,
            Configuration = ToResponse(result.Configuration!)
        });
    }

    [HttpPost]
    public async Task<ActionResult<SopRuleResponse>> CreateRule(
        [FromServices] SopRuleRepository repository,
        [FromBody] SaveSopRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeRequest(request, out var input, out var error))
        {
            return BadRequest(new ErrorResponse { Error = error });
        }

        var created = await repository.CreateRuleAsync(input, cancellationToken);
        return Created($"/api/sop-rules/{created.Id}", ToResponse(created));
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<ActionResult<SopRuleResponse>> UpdateRule(
        [FromServices] SopRuleRepository repository,
        Guid ruleId,
        [FromBody] SaveSopRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeRequest(request, out var input, out var error))
        {
            return BadRequest(new ErrorResponse { Error = error });
        }

        var updated = await repository.UpdateRuleAsync(ruleId, input, cancellationToken);
        return updated is null
            ? NotFound(new ErrorResponse { Error = "SOP rule was not found." })
            : Ok(ToResponse(updated));
    }

    // Chỉ Admin được phép vô hiệu hóa SOP rule.
    [HttpDelete("{ruleId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteRule(
        [FromServices] SopRuleRepository repository,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteRuleAsync(ruleId, cancellationToken);
        return deleted ? NoContent() : NotFound(new ErrorResponse { Error = "SOP rule was not found." });
    }

    /// Đọc user ID từ JWT cho mục đích audit
    /// Hỗ trợ cả claim gốc và claim đã được ASP.NET ánh xạ
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var rawUserId =
            user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(rawUserId, out var userId)
            ? userId
            : null;
    }

    private static bool TryNormalizeRequest(
        SaveSopRuleRequest request,
        out SaveSopRuleReadModel input,
        out string error)
    {
        input = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(request.RuleCode) || string.IsNullOrWhiteSpace(request.RuleName))
        {
            error = "Rule code and rule name are required.";
            return false;
        }

        var actionConfigText = string.IsNullOrWhiteSpace(request.ActionConfigText)
            ? SopRuleAutomationPolicy.CreateActionConfig(
                request.ActionType,
                request.RuleName,
                request.TriggerRiskLevel)
            : request.ActionConfigText.Trim();

        var executionOrder = request.ExecutionOrder
            ?? SopRuleAutomationPolicy.GetExecutionOrder(
                request.ActionType);

        try
        {
            using var _ = JsonDocument.Parse(actionConfigText);
        }
        catch (JsonException)
        {
            error = "Action config must be valid JSON.";
            return false;
        }

        input = new SaveSopRuleReadModel(
            request.RuleCode,
            request.RuleName,
            request.Description,
            request.TriggerRiskLevel,
            request.PreviousRiskLevel,
            request.AppliesToZoneType,
            request.ActionType,
            actionConfigText,
            executionOrder,
            request.IsActive,
            request.ChangeReason);
        return true;
    }

    private static SopRulesResponse ToResponse(SopRulesReadModel rules)
    {
        return new SopRulesResponse
        {
            Summary = new SopRulesSummaryResponse
            {
                TotalRules = rules.Summary.TotalRules,
                ActiveRules = rules.Summary.ActiveRules,
                RecentExecutions = rules.Summary.RecentExecutions,
                AutomatedTasks = rules.Summary.AutomatedTasks
            },
            Rules = rules.Rules.Select(ToResponse).ToList(),
            Executions = rules.Executions.Select(item => new SopExecutionResponse
            {
                Id = item.Id,
                RuleCode = item.RuleCode,
                RuleName = item.RuleName,
                RiskLevel = item.RiskLevel,
                ZoneName = item.ZoneName,
                ActionType = item.ActionType,
                Status = item.Status,
                CompletedAt = item.CompletedAt
            }).ToList()
        };
    }

    private static SopRuleResponse ToResponse(SopRuleReadModel rule)
    {
        using var document = JsonDocument.Parse(rule.ActionConfigText);
        return new SopRuleResponse
        {
            Id = rule.Id,
            RuleCode = rule.RuleCode,
            RuleName = rule.RuleName,
            Description = rule.Description,
            TriggerRiskLevel = rule.TriggerRiskLevel,
            PreviousRiskLevel = rule.PreviousRiskLevel,
            AppliesToZoneType = rule.AppliesToZoneType,
            ActionType = rule.ActionType,
            ActionConfig = document.RootElement.Clone(),
            ActionConfigText = rule.ActionConfigText,
            ExecutionOrder = rule.ExecutionOrder,
            IsActive = rule.IsActive,
            Version = rule.Version,
            ExecutionCount = rule.ExecutionCount,
            UpdatedAt = rule.UpdatedAt
        };
    }
}
