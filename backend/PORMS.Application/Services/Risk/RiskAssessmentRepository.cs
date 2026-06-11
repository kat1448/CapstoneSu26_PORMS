using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public sealed class RiskAssessmentRepository : IRiskAssessmentRepository
{
    private readonly IApplicationDbContext _dbContext;

    public RiskAssessmentRepository(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RiskAssessment?> GetLatestAsync(
        Guid portId,
        bool isSimulation,
        CancellationToken cancellationToken = default)
        => _dbContext.RiskAssessments
            .AsNoTracking()
            .Where(x => x.PortId == portId && x.IsSimulation == isSimulation)
            .OrderByDescending(x => x.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveAsync(
        RiskAssessment assessment,
        IEnumerable<RiskAssessmentDetail> details,
        CancellationToken cancellationToken = default)
    {
        _dbContext.RiskAssessments.Add(assessment);
        _dbContext.RiskAssessmentDetails.AddRange(details);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
