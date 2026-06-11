using System.Text.Json;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Tasks;

public sealed class TaskGeneratorService : ITaskGeneratorService
{
    private readonly IApplicationDbContext _dbContext;

    public TaskGeneratorService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TaskLog> GenerateAsync(
        Guid portId,
        Guid? zoneId,
        Guid sopRuleId,
        Guid? riskAssessmentId,
        SopActionType actionType,
        string actionDescription,
        RiskLevel riskLevel,
        bool isSimulation,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new TaskLog
        {
            Id = Guid.NewGuid(),
            PortId = portId,
            ZoneId = zoneId,
            TriggeredByRuleId = sopRuleId,
            TriggeredByAssessmentId = riskAssessmentId,
            ActionType = actionType,
            ActionDescription = actionDescription,
            RiskLevelAtCreation = riskLevel,
            CreatedAt = now,
            IsSimulation = isSimulation
        };

        _dbContext.TaskLogs.Add(task);
        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = portId,
            EventType = OperationEventType.TASK_CREATED,
            Payload = JsonSerializer.Serialize(new
            {
                taskId = task.Id,
                zoneId,
                sopRuleId,
                riskAssessmentId,
                actionType = actionType.ToString(),
                riskLevel = riskLevel.ToString()
            }),
            Summary = $"SOP task created: {actionDescription}",
            OccurredAt = now,
            IsSimulation = isSimulation
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }
}
