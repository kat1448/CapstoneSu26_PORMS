using System.Text.Json;
using PORMS.API.Services;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class SopRuleAutomationPolicyTests
{
    [Theory]
    [InlineData("SEND_ALERT", 10)]
    [InlineData("RESTRICT_ZONE", 20)]
    [InlineData("SET_LIMITED_MODE", 20)]
    [InlineData("STOP_OPERATIONS", 20)]
    [InlineData("CREATE_TASK", 30)]
    [InlineData("UNRESTRICT_ZONE", 40)]
    [InlineData("SET_NORMAL_MODE", 40)]
    [InlineData("UNKNOWN", 100)]
    public void GetExecutionOrder_ReturnsExpectedOrder(
        string actionType,
        short expected)
    {
        Assert.Equal(
            expected,
            SopRuleAutomationPolicy.GetExecutionOrder(actionType));
    }

    [Fact]
    public void CreateActionConfig_ForTask_UsesRuleNameAndRiskLevel()
    {
        var json = SopRuleAutomationPolicy.CreateActionConfig(
            "CREATE_TASK",
            "Kiểm tra cầu cảng",
            "HIGH");

        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "Kiểm tra cầu cảng",
            document.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "HIGH",
            document.RootElement.GetProperty("priority").GetString());
    }

    [Theory]
    [InlineData("SET_LIMITED_MODE", "LIMITED")]
    [InlineData("STOP_OPERATIONS", "STOP")]
    [InlineData("SET_NORMAL_MODE", "NORMAL")]
    public void CreateActionConfig_ForModeAction_UsesExpectedMode(
        string actionType,
        string expectedMode)
    {
        var json = SopRuleAutomationPolicy.CreateActionConfig(
            actionType,
            "Quy tắc vận hành",
            "HIGH");

        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            expectedMode,
            document.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public void CreateActionConfig_ForSimpleAction_ReturnsEmptyObject()
    {
        Assert.Equal(
            "{}",
            SopRuleAutomationPolicy.CreateActionConfig(
                "SEND_ALERT",
                "Gửi cảnh báo",
                "MEDIUM"));
    }
}
