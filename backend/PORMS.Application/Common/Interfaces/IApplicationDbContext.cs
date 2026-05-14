using Microsoft.EntityFrameworkCore;
using PORMS.Domain.Entities;

namespace PORMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Port> Ports { get; }
    DbSet<WeatherReading> WeatherReadings { get; }
    DbSet<RiskAssessment> RiskAssessments { get; }
    DbSet<RiskThreshold> RiskThresholds { get; }
    DbSet<OperationEvent> OperationEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
