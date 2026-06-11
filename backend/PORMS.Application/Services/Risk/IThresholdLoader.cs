using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public interface IThresholdLoader
{
    Task<IReadOnlyList<RiskThreshold>> GetThresholdsAsync(
        Guid portId,
        CancellationToken cancellationToken = default);

    void InvalidateCache();
}
