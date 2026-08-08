using System.Text.Json;

namespace PORMS.API.Contracts;

public sealed class SopRulesResponse
{
    public required SopRulesSummaryResponse Summary { get; init; }
    public required IReadOnlyList<SopRuleResponse> Rules { get; init; }
    public required IReadOnlyList<SopExecutionResponse> Executions { get; init; }
}

public sealed class SopRulesSummaryResponse
{
    public required long TotalRules { get; init; }
    public required long ActiveRules { get; init; }
    public required long RecentExecutions { get; init; }
    public required long AutomatedTasks { get; init; }
}

public sealed class SopRuleResponse
{
    public required Guid Id { get; init; }
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public string? Description { get; init; }
    public required string TriggerRiskLevel { get; init; }
    public string? PreviousRiskLevel { get; init; }
    public string? AppliesToZoneType { get; init; }
    public required string ActionType { get; init; }
    public required JsonElement ActionConfig { get; init; }
    public required string ActionConfigText { get; init; }
    public required short ExecutionOrder { get; init; }
    public required bool IsActive { get; init; }
    public required int Version { get; init; }
    public required long ExecutionCount { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class SopExecutionResponse
{
    public required Guid Id { get; init; }
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public required string RiskLevel { get; init; }
    public string? ZoneName { get; init; }
    public required string ActionType { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed class SaveSopRuleRequest
{
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public string? Description { get; init; }
    public required string TriggerRiskLevel { get; init; }
    public string? PreviousRiskLevel { get; init; }
    public string? AppliesToZoneType { get; init; }
    public required string ActionType { get; init; }
    // Bỏ trống để backend sinh cấu hình phù hợp với ActionType.
    public string? ActionConfigText { get; init; }

    // Bỏ trống để backend áp dụng thứ tự thực thi mặc định.
    public short? ExecutionOrder { get; init; }
    public bool IsActive { get; init; } = true;
    public string? ChangeReason { get; init; }
}
