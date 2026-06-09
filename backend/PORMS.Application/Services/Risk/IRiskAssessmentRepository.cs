using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public interface IRiskAssessmentRepository
{
    Task<RiskAssessment?> GetLatestAsync(
        Guid portId,
        bool isSimulation,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        RiskAssessment assessment,
        IEnumerable<RiskAssessmentDetail> details,
        CancellationToken cancellationToken = default);
}
