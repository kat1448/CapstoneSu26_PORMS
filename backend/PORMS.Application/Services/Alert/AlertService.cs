using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Alert;

public sealed class AlertService : IAlertService
{
    private static readonly TimeSpan AntiSpamWindow = TimeSpan.FromMinutes(10);
    private readonly IApplicationDbContext _dbContext;

    public AlertService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Domain.Entities.Alert?> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!bypassAntiSpam && severity != AlertSeverity.CRITICAL)
        {
            var duplicateSince = now.Subtract(AntiSpamWindow);
            var hasRecentDuplicate = await _dbContext.Alerts
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PortId == portId &&
                    x.AlertType == alertType &&
                    x.RelatedSopRuleId == relatedSopRuleId &&
                    x.CreatedAt >= duplicateSince &&
                    x.IsSimulation == isSimulation,
                    cancellationToken);

            if (hasRecentDuplicate)
            {
                return null;
            }
        }

        var alert = new Domain.Entities.Alert
        {
            Id = Guid.NewGuid(),
            PortId = portId,
            AlertType = alertType,
            Severity = severity,
            Title = title,
            Message = message,
            Metadata = metadata,
            RelatedSopRuleId = relatedSopRuleId,
            RelatedAssessmentId = relatedAssessmentId,
            CreatedAt = now,
            IsSimulation = isSimulation
        };

        _dbContext.Alerts.Add(alert);
        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = portId,
            EventType = OperationEventType.ALERT_CREATED,
            Payload = JsonSerializer.Serialize(new
            {
                alertId = alert.Id,
                alertType,
                severity = severity.ToString(),
                relatedSopRuleId,
                relatedAssessmentId
            }),
            Summary = $"{severity} alert created: {title}",
            OccurredAt = now,
            IsSimulation = isSimulation
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task<Domain.Entities.Alert?> MarkReadAsync(
        Guid alertId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            return null;
        }

        if (alert.ReadAt is null)
        {
            alert.ReadAt = DateTimeOffset.UtcNow;
            alert.ReadByUserId = userId;
            _dbContext.OperationEvents.Add(new OperationEvent
            {
                Id = Guid.NewGuid(),
                PortId = alert.PortId,
                EventType = OperationEventType.ALERT_READ,
                ActorUserId = userId,
                Payload = JsonSerializer.Serialize(new { alertId }),
                Summary = $"Alert {alertId} marked as read.",
                OccurredAt = alert.ReadAt.Value,
                IsSimulation = alert.IsSimulation
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return alert;
    }

    public async Task<int> MarkAllReadAsync(Guid portId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = await _dbContext.Alerts
            .Where(x => x.PortId == portId && x.ReadAt == null && !x.IsSimulation)
            .ToListAsync(cancellationToken);

        foreach (var alert in alerts)
        {
            alert.ReadAt = now;
            alert.ReadByUserId = userId;
        }

        if (alerts.Count > 0)
        {
            _dbContext.OperationEvents.Add(new OperationEvent
            {
                Id = Guid.NewGuid(),
                PortId = portId,
                EventType = OperationEventType.ALERT_READ,
                ActorUserId = userId,
                Payload = JsonSerializer.Serialize(new { count = alerts.Count }),
                Summary = $"{alerts.Count} alerts marked as read.",
                OccurredAt = now,
                IsSimulation = false
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return alerts.Count;
    }
}
