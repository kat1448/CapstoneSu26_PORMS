using PORMS.Application.DTOs.Risk;
using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public interface IRiskThresholdService
{
    Task<IReadOnlyList<RiskThreshold>> GetGlobalThresholdsAsync(
        CancellationToken cancellationToken = default);

    Task<RiskThreshold> UpdateAsync(
        Guid id,
        UpdateRiskThresholdRequest request,
        CancellationToken cancellationToken = default);

    Task<RiskThresholdPreviewResponse> PreviewAsync(
        RiskThresholdPreviewRequest request,
        CancellationToken cancellationToken = default);
}
