using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Mode;

public interface IOperationModeService
{
    Task<OperationModeLog?> GetLatestLogAsync(Guid portId, CancellationToken cancellationToken = default);

    Task<OperationModeLog> ChangeModeAsync(
        Guid portId,
        OperationMode targetMode,
        RiskLevel? riskLevel,
        Guid? sopRuleId,
        bool isSimulation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationModeLog>> ForceStopAsync(
        Guid portId,
        RiskLevel riskLevel,
        Guid sopRuleId,
        bool isSimulation,
        CancellationToken cancellationToken = default);

    Task<OperationModeLog> OverrideModeAsync(
        Guid portId,
        OperationMode targetMode,
        string overrideReason,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
