using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Tasks;

public interface ITaskGeneratorService
{
    Task<TaskLog> GenerateAsync(
        Guid portId,
        Guid? zoneId,
        Guid sopRuleId,
        Guid? riskAssessmentId,
        SopActionType actionType,
        string actionDescription,
        RiskLevel riskLevel,
        bool isSimulation,
        CancellationToken cancellationToken = default);
}
