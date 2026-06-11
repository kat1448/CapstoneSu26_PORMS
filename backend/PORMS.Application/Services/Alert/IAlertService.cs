using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Alert;

public interface IAlertService
{
    Task<global::PORMS.Domain.Entities.Alert?> CreateAsync(
        Guid portId,
        string alertType,
        AlertSeverity severity,
        string title,
        string message,
        Guid? relatedSopRuleId,
        Guid? relatedAssessmentId,
        bool isSimulation,
        string? metadata = null,
        bool bypassAntiSpam = false,
        CancellationToken cancellationToken = default);

    Task<global::PORMS.Domain.Entities.Alert?> MarkReadAsync(
        Guid alertId,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(Guid portId, Guid? userId, CancellationToken cancellationToken = default);
}
