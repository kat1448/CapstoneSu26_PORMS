using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Sop;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/sop-rules")]
public sealed class SopController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private readonly IApplicationDbContext _dbContext;

    public SopController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRulesAsync(
        [FromQuery] RiskLevel? triggerRiskLevel,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.SopRules.AsNoTracking();
        if (triggerRiskLevel.HasValue)
        {
            query = query.Where(x => x.TriggerRiskLevel == triggerRiskLevel.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rules = await query
            .OrderBy(x => x.TriggerRiskLevel)
            .ThenBy(x => x.ExecutionOrder)
            .ThenBy(x => x.RuleName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var stats = await GetRuleStatsAsync(rules.Select(x => x.Id).ToList(), cancellationToken);

        return Ok(new
        {
            data = rules.Select(x => ToDto(x, stats)).ToList(),
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SopRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SopRuleDto>> GetRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.SopRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        var stats = await GetRuleStatsAsync([rule.Id], cancellationToken);
        return Ok(ToDto(rule, stats));
    }

    [HttpPost]
    [ProducesResponseType<SopRuleDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SopRuleDto>> CreateRuleAsync(
        [FromBody] CreateSopRuleRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRuleFields(request.RuleName, request.ActionDescription, request.ExecutionOrder);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var now = DateTimeOffset.UtcNow;
        var rule = new SopRule
        {
            Id = Guid.NewGuid(),
            RuleName = request.RuleName.Trim(),
            TriggerRiskLevel = request.TriggerRiskLevel,
            AppliesToZoneType = request.AppliesToZoneType,
            ActionType = request.ActionType,
            ActionDescription = request.ActionDescription.Trim(),
            TargetOperationMode = request.TargetOperationMode,
            ExecutionOrder = request.ExecutionOrder,
            AlertMessage = string.IsNullOrWhiteSpace(request.AlertMessage) ? null : request.AlertMessage.Trim(),
            AlertSeverity = request.AlertSeverity,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedByUserId = request.UpdatedByUserId
        };

        _dbContext.SopRules.Add(rule);
        AddSopRuleEvent(rule, request.UpdatedByUserId, "SOP rule created.", now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetRuleAsync), new { id = rule.Id }, ToDto(rule));
    }

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> ImportRulesAsync(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var requests = ParseImportPayload(payload);
        if (requests.Count == 0)
        {
            return BadRequest("At least one SOP rule is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var imported = new List<SopRule>();
        foreach (var request in requests)
        {
            var validationError = ValidateRuleFields(request.RuleName, request.ActionDescription, request.ExecutionOrder);
            if (validationError is not null)
            {
                return BadRequest($"{request.RuleName}: {validationError}");
            }

            var rule = new SopRule
            {
                Id = Guid.NewGuid(),
                RuleName = request.RuleName.Trim(),
                TriggerRiskLevel = request.TriggerRiskLevel,
                AppliesToZoneType = request.AppliesToZoneType,
                ActionType = request.ActionType,
                ActionDescription = request.ActionDescription.Trim(),
                TargetOperationMode = request.TargetOperationMode,
                ExecutionOrder = request.ExecutionOrder,
                AlertMessage = string.IsNullOrWhiteSpace(request.AlertMessage) ? null : request.AlertMessage.Trim(),
                AlertSeverity = request.AlertSeverity,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByUserId = request.UpdatedByUserId
            };

            _dbContext.SopRules.Add(rule);
            AddSopRuleEvent(rule, request.UpdatedByUserId, "SOP rule imported.", now);
            imported.Add(rule);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Created(string.Empty, new
        {
            imported = imported.Count,
            data = imported.Select(x => ToDto(x)).ToList()
        });
    }

    [HttpGet("export")]
    [ProducesResponseType<IReadOnlyList<SopRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SopRuleDto>>> ExportRulesAsync(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SopRules.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var rules = await query
            .OrderBy(x => x.TriggerRiskLevel)
            .ThenBy(x => x.ExecutionOrder)
            .ThenBy(x => x.RuleName)
            .ToListAsync(cancellationToken);

        return Ok(rules.Select(x => ToDto(x)).ToList());
    }

    [HttpGet("executions")]
    public async Task<IActionResult> GetExecutionsAsync(
        [FromQuery] Guid? portId,
        [FromQuery] Guid? riskAssessmentId,
        [FromQuery] bool includeSimulation = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.SopExecutions.AsNoTracking();
        if (portId.HasValue)
        {
            query = query.Where(x => x.PortId == portId.Value);
        }

        if (riskAssessmentId.HasValue)
        {
            query = query.Where(x => x.RiskAssessmentId == riskAssessmentId.Value);
        }

        if (!includeSimulation)
        {
            query = query.Where(x => !x.IsSimulation);
        }

        var total = await query.CountAsync(cancellationToken);
        var executions = await query
            .OrderByDescending(x => x.ExecutedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SopExecutionDto(
                x.Id,
                x.RuleId,
                x.RiskAssessmentId,
                x.PortId,
                x.ZoneId,
                x.ExecutedAt,
                x.ExecutionResult,
                x.SkipReason,
                x.DurationMs,
                x.IsSimulation))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = executions,
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    [HttpGet("executions/{id:guid}")]
    [ProducesResponseType<SopExecutionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SopExecutionDto>> GetExecutionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var execution = await _dbContext.SopExecutions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SopExecutionDto(
                x.Id,
                x.RuleId,
                x.RiskAssessmentId,
                x.PortId,
                x.ZoneId,
                x.ExecutedAt,
                x.ExecutionResult,
                x.SkipReason,
                x.DurationMs,
                x.IsSimulation))
            .FirstOrDefaultAsync(cancellationToken);

        return execution is null ? NotFound() : Ok(execution);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<SopRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SopRuleDto>> UpdateRuleAsync(
        Guid id,
        [FromBody] UpdateSopRuleRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRuleFields(request.RuleName, request.ActionDescription, request.ExecutionOrder);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        if (string.IsNullOrWhiteSpace(request.ChangeReason) || request.ChangeReason.Trim().Length < 20)
        {
            return BadRequest("Change reason must contain at least 20 characters.");
        }

        var rule = await _dbContext.SopRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        rule.RuleName = request.RuleName.Trim();
        rule.TriggerRiskLevel = request.TriggerRiskLevel;
        rule.AppliesToZoneType = request.AppliesToZoneType;
        rule.ActionType = request.ActionType;
        rule.ActionDescription = request.ActionDescription.Trim();
        rule.TargetOperationMode = request.TargetOperationMode;
        rule.ExecutionOrder = request.ExecutionOrder;
        rule.AlertMessage = string.IsNullOrWhiteSpace(request.AlertMessage) ? null : request.AlertMessage.Trim();
        rule.AlertSeverity = request.AlertSeverity;
        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.UpdatedByUserId = request.UpdatedByUserId;

        AddSopRuleEvent(rule, request.UpdatedByUserId, request.ChangeReason.Trim(), rule.UpdatedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(rule));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableRuleAsync(
        Guid id,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.SopRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        rule.IsActive = false;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.UpdatedByUserId = userId;
        AddSopRuleEvent(rule, userId, "SOP rule disabled.", rule.UpdatedAt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("active-recommendations")]
    [ProducesResponseType<IReadOnlyList<SopRecommendationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SopRecommendationDto>>> GetActiveRecommendationsAsync(
        [FromQuery] Guid portId,
        [FromQuery] RiskLevel riskLevel,
        CancellationToken cancellationToken)
    {
        var zones = await _dbContext.Zones
            .AsNoTracking()
            .Where(x => x.PortId == portId && x.IsActive)
            .ToListAsync(cancellationToken);

        var rules = await _dbContext.SopRules
            .AsNoTracking()
            .Where(x => x.IsActive && x.TriggerRiskLevel == riskLevel)
            .OrderBy(x => x.ExecutionOrder)
            .ThenBy(x => x.RuleName)
            .ToListAsync(cancellationToken);

        var recommendations = new List<SopRecommendationDto>();
        foreach (var rule in rules)
        {
            var targets = rule.AppliesToZoneType is null
                ? [null]
                : zones.Where(x => x.ZoneType == rule.AppliesToZoneType).Cast<Zone?>().ToList();

            foreach (var zone in targets)
            {
                recommendations.Add(new SopRecommendationDto(
                    rule.Id,
                    rule.RuleName,
                    zone?.Id,
                    zone?.Name,
                    zone?.ZoneType,
                    rule.TriggerRiskLevel,
                    rule.ActionType,
                    rule.ActionDescription,
                    rule.TargetOperationMode,
                    rule.ExecutionOrder,
                    rule.AlertSeverity,
                    rule.AlertMessage));
            }
        }

        return Ok(recommendations);
    }

    private void AddSopRuleEvent(SopRule rule, Guid? userId, string summary, DateTimeOffset occurredAt)
    {
        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = null,
            EventType = OperationEventType.SOP_RULE_UPDATED,
            ActorUserId = userId,
            Payload = JsonSerializer.Serialize(new
            {
                sopRuleId = rule.Id,
                rule.RuleName,
                triggerRiskLevel = rule.TriggerRiskLevel.ToString(),
                actionType = rule.ActionType.ToString(),
                rule.IsActive
            }),
            Summary = summary,
            OccurredAt = occurredAt,
            IsSimulation = false
        });
    }

    private async Task<IReadOnlyDictionary<Guid, RuleExecutionStats>> GetRuleStatsAsync(
        IReadOnlyCollection<Guid> ruleIds,
        CancellationToken cancellationToken)
    {
        if (ruleIds.Count == 0)
        {
            return new Dictionary<Guid, RuleExecutionStats>();
        }

        return await _dbContext.SopExecutions
            .AsNoTracking()
            .Where(x => ruleIds.Contains(x.RuleId) && !x.IsSimulation)
            .GroupBy(x => x.RuleId)
            .Select(x => new
            {
                RuleId = x.Key,
                TotalExecutions = x.Count(),
                LastTriggeredAt = x.Max(e => e.ExecutedAt)
            })
            .ToDictionaryAsync(
                x => x.RuleId,
                x => new RuleExecutionStats(x.TotalExecutions, x.LastTriggeredAt),
                cancellationToken);
    }

    private static SopRuleDto ToDto(
        SopRule rule,
        IReadOnlyDictionary<Guid, RuleExecutionStats>? stats = null)
    {
        RuleExecutionStats? ruleStats = null;
        stats?.TryGetValue(rule.Id, out ruleStats);

        return new(
            rule.Id,
            rule.RuleName,
            rule.TriggerRiskLevel,
            rule.AppliesToZoneType,
            rule.ActionType,
            rule.ActionDescription,
            rule.TargetOperationMode,
            rule.ExecutionOrder,
            rule.AlertMessage,
            rule.AlertSeverity,
            rule.IsActive,
            rule.CreatedAt,
            rule.UpdatedAt,
            ruleStats?.TotalExecutions ?? 0,
            ruleStats?.LastTriggeredAt);
    }

    private sealed record RuleExecutionStats(int TotalExecutions, DateTimeOffset LastTriggeredAt);

    private static string? ValidateRuleFields(string ruleName, string actionDescription, short executionOrder)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            return "Rule name is required.";
        }

        if (string.IsNullOrWhiteSpace(actionDescription))
        {
            return "Action description is required.";
        }

        return executionOrder <= 0 ? "Execution order must be greater than 0." : null;
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

    private static IReadOnlyList<CreateSopRuleRequest> ParseImportPayload(JsonElement payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());

        return payload.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<List<CreateSopRuleRequest>>(
                payload.GetRawText(),
                options) ?? [],
            JsonValueKind.Object => [JsonSerializer.Deserialize<CreateSopRuleRequest>(
                payload.GetRawText(),
                options) ?? throw new JsonException("Invalid SOP rule import payload.")],
            _ => []
        };
    }
}
