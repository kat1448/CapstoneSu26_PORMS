using Microsoft.EntityFrameworkCore;
using PORMS.Domain.Entities;

namespace PORMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Alert> Alerts { get; }
    DbSet<OperationModeLog> OperationModeLogs { get; }
    DbSet<OperationEvent> OperationEvents { get; }
    DbSet<Port> Ports { get; }
    DbSet<RiskAssessment> RiskAssessments { get; }
    DbSet<RiskAssessmentDetail> RiskAssessmentDetails { get; }
    DbSet<RiskThreshold> RiskThresholds { get; }
    DbSet<SopExecution> SopExecutions { get; }
    DbSet<SopRule> SopRules { get; }
    DbSet<SimulationSession> SimulationSessions { get; }
    DbSet<TaskLog> TaskLogs { get; }
    DbSet<User> Users { get; }
    DbSet<WeatherReading> WeatherReadings { get; }
    DbSet<WeatherFetchJob> WeatherFetchJobs { get; }
    DbSet<Zone> Zones { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
