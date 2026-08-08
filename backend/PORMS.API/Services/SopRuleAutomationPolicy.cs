using System.Text.Json;

namespace PORMS.API.Services;

/// <summary>
/// Sinh cấu hình kỹ thuật mặc định cho biểu mẫu SOP đơn giản.
/// File Excel nâng cao vẫn có thể cung cấp giá trị riêng khi cần.
/// </summary>
public static class SopRuleAutomationPolicy
{
    public static string CreateActionConfig(
        string? actionType,
        string? ruleName,
        string? triggerRiskLevel)
    {
        var normalizedAction = actionType?.Trim().ToUpperInvariant();
        var normalizedName = ruleName?.Trim() ?? string.Empty;
        var normalizedRisk = triggerRiskLevel?.Trim().ToUpperInvariant()
            ?? string.Empty;

        return normalizedAction switch
        {
            "CREATE_TASK" => JsonSerializer.Serialize(new
            {
                title = normalizedName,
                priority = normalizedRisk
            }),
            "SET_LIMITED_MODE" => JsonSerializer.Serialize(new
            {
                mode = "LIMITED",
                sendAlert = true
            }),
            "STOP_OPERATIONS" => JsonSerializer.Serialize(new
            {
                mode = "STOP",
                sendAlert = true,
                createTask = true
            }),
            "SET_NORMAL_MODE" => JsonSerializer.Serialize(new
            {
                mode = "NORMAL",
                requiresInspection = true
            }),
            _ => "{}"
        };
    }

    public static short GetExecutionOrder(string? actionType)
    {
        return actionType?.Trim().ToUpperInvariant() switch
        {
            "SEND_ALERT" => 10,
            "RESTRICT_ZONE" or
            "SET_LIMITED_MODE" or
            "STOP_OPERATIONS" => 20,
            "CREATE_TASK" => 30,
            "UNRESTRICT_ZONE" or
            "SET_NORMAL_MODE" => 40,
            _ => 100
        };
    }
}
