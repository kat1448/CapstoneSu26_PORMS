using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Mode;

public sealed class OperationModeService : IOperationModeService
{
    private readonly IApplicationDbContext _dbContext;

    public OperationModeService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OperationModeLog?> GetLatestLogAsync(Guid portId, CancellationToken cancellationToken = default)
        => _dbContext.OperationModeLogs
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation)
            .OrderByDescending(x => x.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<OperationModeLog> ChangeModeAsync(
        Guid portId,
        OperationMode targetMode,
        RiskLevel? riskLevel,
        Guid? sopRuleId,
        bool isSimulation,
        CancellationToken cancellationToken = default)
    {
        var port = await _dbContext.Ports.FirstOrDefaultAsync(x => x.Id == portId, cancellationToken)
            ?? throw new KeyNotFoundException($"Port {portId} was not found.");

        if (!OperationModeTransitionPolicy.IsAutomaticTransitionAllowed(port.CurrentMode, targetMode))
        {
            throw new InvalidOperationException(
                $"Automatic mode transition {port.CurrentMode} -> {targetMode} is not allowed.");
        }

        return await AddModeLogAsync(
            port,
            targetMode,
            riskLevel,
            sopRuleId,
            overriddenByUserId: null,
            overrideReason: null,
            changeType: "AUTOMATIC",
            isSimulation,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OperationModeLog>> ForceStopAsync(
        Guid portId,
        RiskLevel riskLevel,
        Guid sopRuleId,
        bool isSimulation,
        CancellationToken cancellationToken = default)
    {
        var port = await _dbContext.Ports.FirstOrDefaultAsync(x => x.Id == portId, cancellationToken)
            ?? throw new KeyNotFoundException($"Port {portId} was not found.");

        var logs = new List<OperationModeLog>();
        if (port.CurrentMode == OperationMode.STOP)
        {
            return logs;
        }

        var effectiveMode = port.CurrentMode;

        if (effectiveMode == OperationMode.NORMAL)
        {
            logs.Add(await AddModeLogAsync(
                port,
                OperationMode.LIMITED,
                riskLevel,
                sopRuleId,
                overriddenByUserId: null,
                overrideReason: "SOP engine escalated mode before STOP.",
                changeType: "AUTOMATIC",
                isSimulation,
                cancellationToken,
                previousModeOverride: effectiveMode));
            effectiveMode = OperationMode.LIMITED;
        }

        if (effectiveMode == OperationMode.LIMITED)
        {
            logs.Add(await AddModeLogAsync(
                port,
                OperationMode.STOP,
                riskLevel,
                sopRuleId,
                overriddenByUserId: null,
                overrideReason: "SOP engine forced STOP for high-risk condition.",
                changeType: "AUTOMATIC",
                isSimulation,
                cancellationToken,
                previousModeOverride: effectiveMode));
        }

        return logs;
    }

    public async Task<OperationModeLog> OverrideModeAsync(
        Guid portId,
        OperationMode targetMode,
        string overrideReason,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(overrideReason) || overrideReason.Trim().Length < 20)
        {
            throw new ArgumentException("Override reason must contain at least 20 characters.", nameof(overrideReason));
        }

        var port = await _dbContext.Ports.FirstOrDefaultAsync(x => x.Id == portId, cancellationToken)
            ?? throw new KeyNotFoundException($"Port {portId} was not found.");

        return await AddModeLogAsync(
            port,
            targetMode,
            riskLevel: null,
            sopRuleId: null,
            overriddenByUserId: userId,
            overrideReason: overrideReason.Trim(),
            changeType: "MANUAL",
            isSimulation: false,
            cancellationToken);
    }

    private async Task<OperationModeLog> AddModeLogAsync(
        Port port,
        OperationMode targetMode,
        RiskLevel? riskLevel,
        Guid? sopRuleId,
        Guid? overriddenByUserId,
        string? overrideReason,
        string changeType,
        bool isSimulation,
        CancellationToken cancellationToken,
        OperationMode? previousModeOverride = null)
    {
        var now = DateTimeOffset.UtcNow;
        var previousMode = previousModeOverride ?? port.CurrentMode;

        if (!isSimulation)
        {
            port.CurrentMode = targetMode;
            port.UpdatedAt = now;
        }

        var log = new OperationModeLog
        {
            Id = Guid.NewGuid(),
            PortId = port.Id,
            PreviousMode = previousMode,
            NewMode = targetMode,
            TriggeredByRiskLevel = riskLevel,
            TriggeredBySopRuleId = sopRuleId,
            OverriddenByUserId = overriddenByUserId,
            OverrideReason = overrideReason,
            ChangeType = changeType,
            ChangedAt = now,
            IsSimulation = isSimulation
        };

        _dbContext.OperationModeLogs.Add(log);
        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = port.Id,
            EventType = changeType == "MANUAL" ? OperationEventType.MODE_OVERRIDDEN : OperationEventType.MODE_CHANGED,
            ActorUserId = overriddenByUserId,
            Payload = JsonSerializer.Serialize(new
            {
                previousMode = previousMode.ToString(),
                newMode = targetMode.ToString(),
                riskLevel = riskLevel?.ToString(),
                sopRuleId,
                overrideReason
            }),
            Summary = $"Operation mode changed from {previousMode} to {targetMode}.",
            OccurredAt = now,
            IsSimulation = isSimulation
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return log;
    }
}
