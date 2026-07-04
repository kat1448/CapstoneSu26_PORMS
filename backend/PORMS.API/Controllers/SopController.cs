using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;
using System.Text.Json;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOrSuperAdmin")]
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

    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(
        [FromServices] SopRuleRepository repository,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteRuleAsync(ruleId, cancellationToken);
        return deleted ? NoContent() : NotFound(new ErrorResponse { Error = "SOP rule was not found." });
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
            ? "{}"
            : request.ActionConfigText.Trim();

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
            request.ExecutionOrder,
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
