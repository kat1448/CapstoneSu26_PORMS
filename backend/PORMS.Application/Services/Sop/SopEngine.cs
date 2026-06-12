using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.Services.Alert;
using PORMS.Application.Services.Mode;
using PORMS.Application.Services.Tasks;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Domain.Events;

namespace PORMS.Application.Services.Sop;

public sealed class SopEngine : ISopEngine
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOperationModeService _operationModeService;
    private readonly IAlertService _alertService;
    private readonly ITaskGeneratorService _taskGeneratorService;
    private readonly ILogger<SopEngine> _logger;

    public SopEngine(
        IApplicationDbContext dbContext,
        IOperationModeService operationModeService,
        IAlertService alertService,
        ITaskGeneratorService taskGeneratorService,
        ILogger<SopEngine> logger)
    {
        _dbContext = dbContext;
        _operationModeService = operationModeService;
        _alertService = alertService;
        _taskGeneratorService = taskGeneratorService;
        _logger = logger;
    }

    public async Task HandleRiskChangedAsync(
        RiskChangedEvent riskChangedEvent,
        CancellationToken cancellationToken = default)
    {
        var port = await _dbContext.Ports
            .FirstOrDefaultAsync(x => x.Id == riskChangedEvent.PortId, cancellationToken);
        if (port is null)
        {
            _logger.LogWarning("SOP engine skipped because port {PortId} was not found.", riskChangedEvent.PortId);
            return;
        }

        if (!riskChangedEvent.IsSimulation)
        {
            port.CurrentRiskLevel = riskChangedEvent.NewLevel;
            port.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var rules = await _dbContext.SopRules
            .AsNoTracking()
            .Where(x => x.IsActive && x.TriggerRiskLevel == riskChangedEvent.NewLevel)
            .OrderBy(x => x.ExecutionOrder)
            .ThenBy(x => x.RuleName)
            .ToListAsync(cancellationToken);

        var zones = await _dbContext.Zones
            .AsNoTracking()
            .Where(x => x.PortId == riskChangedEvent.PortId && x.IsActive)
            .ToListAsync(cancellationToken);

        var executed = 0;
        var skipped = 0;
        var failed = 0;
        var alertsCreated = 0;
        var tasksCreated = 0;
        var modeChanges = 0;

        foreach (var rule in rules)
        {
            var targets = ResolveTargets(rule, zones);
            if (targets.Count == 0)
            {
                skipped++;
                AddExecution(
                    rule,
                    riskChangedEvent,
                    zone: null,
                    status: "SKIPPED",
                    alertCreated: false,
                    taskCreated: false,
                    modeChanges: 0,
                    skipReason: $"No active zone matched {rule.AppliesToZoneType}.",
                    durationMs: 0);
                continue;
            }

            foreach (var zone in targets)
            {
                var startedAt = DateTimeOffset.UtcNow;
                var taskCreated = false;
                var alertCreated = false;
                var executionModeChanges = 0;
                try
                {
                    await _taskGeneratorService.GenerateAsync(
                        riskChangedEvent.PortId,
                        zone?.Id,
                        rule.Id,
                        riskChangedEvent.RiskAssessmentId,
                        rule.ActionType,
                        BuildActionDescription(rule, zone),
                        riskChangedEvent.NewLevel,
                        riskChangedEvent.IsSimulation,
                        cancellationToken);
                    tasksCreated++;
                    taskCreated = true;

                    var alert = await _alertService.CreateAsync(
                        riskChangedEvent.PortId,
                        $"SOP_{rule.ActionType}",
                        rule.AlertSeverity,
                        rule.RuleName,
                        rule.AlertMessage ?? BuildActionDescription(rule, zone),
                        rule.Id,
                        riskChangedEvent.RiskAssessmentId,
                        riskChangedEvent.IsSimulation,
                        JsonSerializer.Serialize(new
                        {
                            zoneId = zone?.Id,
                            zoneName = zone?.Name,
                            zoneType = zone?.ZoneType.ToString(),
                            riskLevel = riskChangedEvent.NewLevel.ToString(),
                            targetOperationMode = rule.TargetOperationMode?.ToString()
                        }),
                        bypassAntiSpam: rule.AlertSeverity == AlertSeverity.CRITICAL,
                        cancellationToken);

                    if (alert is not null)
                    {
                        alertsCreated++;
                        alertCreated = true;
                    }

                    executionModeChanges = await ApplyModeChangeAsync(rule, riskChangedEvent, cancellationToken);
                    modeChanges += executionModeChanges;
                    executed++;
                    AddExecution(
                        rule,
                        riskChangedEvent,
                        zone,
                        status: "EXECUTED",
                        alertCreated,
                        taskCreated,
                        executionModeChanges,
                        skipReason: null,
                        durationMs: GetElapsedMilliseconds(startedAt));
                }
                catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
                {
                    failed++;
                    AddExecution(
                        rule,
                        riskChangedEvent,
                        zone,
                        status: "FAILED",
                        alertCreated,
                        taskCreated,
                        executionModeChanges,
                        skipReason: exception.Message,
                        durationMs: GetElapsedMilliseconds(startedAt));
                    _logger.LogWarning(
                        exception,
                        "SOP rule {RuleId} failed for port {PortId}.",
                        rule.Id,
                        riskChangedEvent.PortId);
                }
            }
        }

        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = riskChangedEvent.PortId,
            EventType = OperationEventType.SOP_TRIGGERED,
            Payload = JsonSerializer.Serialize(new
            {
                riskAssessmentId = riskChangedEvent.RiskAssessmentId,
                oldRiskLevel = riskChangedEvent.OldLevel?.ToString(),
                newRiskLevel = riskChangedEvent.NewLevel.ToString(),
                ruleCount = rules.Count,
                executed,
                skipped,
                failed,
                alertsCreated,
                tasksCreated,
                modeChanges
            }),
            Summary = $"SOP engine handled {riskChangedEvent.NewLevel}: {executed} executed, {skipped} skipped, {failed} failed.",
            OccurredAt = DateTimeOffset.UtcNow,
            IsSimulation = riskChangedEvent.IsSimulation
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ApplyModeChangeAsync(
        SopRule rule,
        RiskChangedEvent riskChangedEvent,
        CancellationToken cancellationToken)
    {
        if (rule.TargetOperationMode is null)
        {
            return 0;
        }

        if (rule.TargetOperationMode == OperationMode.NORMAL)
        {
            _logger.LogInformation(
                "SOP recovery rule {RuleId} recommends NORMAL mode. Automatic resume is intentionally not applied.",
                rule.Id);
            return 0;
        }

        if (rule.TargetOperationMode == OperationMode.STOP)
        {
            var logs = await _operationModeService.ForceStopAsync(
                riskChangedEvent.PortId,
                riskChangedEvent.NewLevel,
                rule.Id,
                riskChangedEvent.IsSimulation,
                cancellationToken);
            return logs.Count;
        }

        await _operationModeService.ChangeModeAsync(
            riskChangedEvent.PortId,
            rule.TargetOperationMode.Value,
            riskChangedEvent.NewLevel,
            rule.Id,
            riskChangedEvent.IsSimulation,
            cancellationToken);
        return 1;
    }

    private static IReadOnlyList<global::PORMS.Domain.Entities.Zone?> ResolveTargets(
        SopRule rule,
        IReadOnlyCollection<global::PORMS.Domain.Entities.Zone> zones)
    {
        if (rule.AppliesToZoneType is null)
        {
            return [null];
        }

        return zones
            .Where(x => x.ZoneType == rule.AppliesToZoneType)
            .Cast<global::PORMS.Domain.Entities.Zone?>()
            .ToList();
    }

    private static string BuildActionDescription(SopRule rule, global::PORMS.Domain.Entities.Zone? zone)
        => zone is null
            ? rule.ActionDescription
            : $"{rule.ActionDescription} Zone: {zone.Name} ({zone.ZoneType}).";

    private void AddExecution(
        SopRule rule,
        RiskChangedEvent riskChangedEvent,
        global::PORMS.Domain.Entities.Zone? zone,
        string status,
        bool alertCreated,
        bool taskCreated,
        int modeChanges,
        string? skipReason,
        int durationMs)
    {
        _dbContext.SopExecutions.Add(new SopExecution
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            RiskAssessmentId = riskChangedEvent.RiskAssessmentId,
            PortId = riskChangedEvent.PortId,
            ZoneId = zone?.Id,
            ExecutedAt = DateTimeOffset.UtcNow,
            ExecutionResult = JsonSerializer.Serialize(new
            {
                status,
                actionType = rule.ActionType.ToString(),
                riskLevel = riskChangedEvent.NewLevel.ToString(),
                zoneId = zone?.Id,
                zoneName = zone?.Name,
                alertCreated,
                taskCreated,
                modeChanges,
                targetOperationMode = rule.TargetOperationMode?.ToString()
            }),
            SkipReason = skipReason,
            DurationMs = durationMs,
            IsSimulation = riskChangedEvent.IsSimulation
        });
    }

    private static int GetElapsedMilliseconds(DateTimeOffset startedAt)
        => Math.Max(0, (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
}
