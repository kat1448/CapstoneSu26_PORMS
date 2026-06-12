using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Mode;

public static class OperationModeTransitionPolicy
{
    public static bool IsAutomaticTransitionAllowed(OperationMode currentMode, OperationMode targetMode)
        => (currentMode, targetMode) switch
        {
            (_, var target) when currentMode == target => true,
            (OperationMode.NORMAL, OperationMode.LIMITED) => true,
            (OperationMode.LIMITED, OperationMode.STOP) => true,
            _ => false
        };
}
