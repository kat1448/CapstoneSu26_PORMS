using PORMS.Application.Services.Mode;
using PORMS.Domain.Enums;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class OperationModeTests
{
    [Theory]
    [InlineData(OperationMode.NORMAL, OperationMode.LIMITED, true)]
    [InlineData(OperationMode.LIMITED, OperationMode.STOP, true)]
    [InlineData(OperationMode.NORMAL, OperationMode.NORMAL, true)]
    [InlineData(OperationMode.LIMITED, OperationMode.LIMITED, true)]
    [InlineData(OperationMode.STOP, OperationMode.STOP, true)]
    [InlineData(OperationMode.NORMAL, OperationMode.STOP, false)]
    [InlineData(OperationMode.STOP, OperationMode.NORMAL, false)]
    [InlineData(OperationMode.LIMITED, OperationMode.NORMAL, false)]
    public void AutomaticTransitionPolicy_OnlyAllowsForwardEscalation(
        OperationMode currentMode,
        OperationMode targetMode,
        bool expected)
    {
        var actual = OperationModeTransitionPolicy.IsAutomaticTransitionAllowed(currentMode, targetMode);

        Assert.Equal(expected, actual);
    }
}
