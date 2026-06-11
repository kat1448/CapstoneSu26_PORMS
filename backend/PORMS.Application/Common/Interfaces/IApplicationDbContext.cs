using Microsoft.EntityFrameworkCore;
using PORMS.Domain.Entities;

namespace PORMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<OperationEvent> OperationEvents { get; }
    DbSet<Port> Ports { get; }
    DbSet<RiskAssessment> RiskAssessments { get; }
    DbSet<RiskAssessmentDetail> RiskAssessmentDetails { get; }
    DbSet<RiskThreshold> RiskThresholds { get; }
    DbSet<User> Users { get; }
    DbSet<WeatherReading> WeatherReadings { get; }
    DbSet<WeatherFetchJob> WeatherFetchJobs { get; }
    DbSet<Zone> Zones { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
