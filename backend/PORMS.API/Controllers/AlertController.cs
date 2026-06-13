using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Alerts;
using PORMS.Application.Services.Alert;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;
    private readonly IAlertService _alertService;

    public AlertController(IApplicationDbContext dbContext, IAlertService alertService)
    {
        _dbContext = dbContext;
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlertsAsync(
        [FromQuery] Guid? portId,
        [FromQuery] AlertSeverity? severity,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.Alerts.AsNoTracking().Where(x => !x.IsSimulation);
        if (portId.HasValue)
        {
            query = query.Where(x => x.PortId == portId.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(x => x.Severity == severity.Value);
        }

        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var alerts = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = alerts,
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCountAsync(
        [FromQuery] Guid? portId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Alerts.AsNoTracking().Where(x => x.ReadAt == null && !x.IsSimulation);
        if (portId.HasValue)
        {
            query = query.Where(x => x.PortId == portId.Value);
        }

        var count = await query.CountAsync(cancellationToken);
        return Ok(new { unreadCount = count });
    }

    [HttpGet("stats")]
    [ProducesResponseType<AlertStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertStatsDto>> GetStatsAsync(
        [FromQuery] Guid? portId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var todayQuery = _dbContext.Alerts
            .AsNoTracking()
            .Where(x => !x.IsSimulation && x.CreatedAt >= dayStart && x.CreatedAt < dayEnd);
        var allQuery = _dbContext.Alerts
            .AsNoTracking()
            .Where(x => !x.IsSimulation);

        if (portId.HasValue)
        {
            todayQuery = todayQuery.Where(x => x.PortId == portId.Value);
            allQuery = allQuery.Where(x => x.PortId == portId.Value);
        }

        var todayAlerts = await todayQuery.ToListAsync(cancellationToken);
        var unread = await allQuery.CountAsync(x => x.ReadAt == null, cancellationToken);
        var readDurations = todayAlerts
            .Where(x => x.ReadAt.HasValue)
            .Select(x => (x.ReadAt!.Value - x.CreatedAt).TotalMinutes)
            .ToList();

        var bySeverity = todayAlerts
            .GroupBy(x => x.Severity)
            .ToDictionary(x => x.Key, x => x.Count());

        return Ok(new AlertStatsDto(
            portId,
            targetDate,
            todayAlerts.Count,
            unread,
            todayAlerts.Count(x => x.Severity == AlertSeverity.CRITICAL),
            todayAlerts.Count(x => x.ReadAt.HasValue),
            readDurations.Count == 0 ? null : Math.Round(readDurations.Average(), 2),
            bySeverity));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AlertDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertDto>> GetAlertAsync(Guid id, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Alerts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return alert is null ? NotFound() : Ok(ToDto(alert));
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType<AlertDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertDto>> MarkReadAsync(
        Guid id,
        [FromBody] MarkAlertReadRequest? request,
        CancellationToken cancellationToken)
    {
        var alert = await _alertService.MarkReadAsync(id, request?.UserId, cancellationToken);
        return alert is null ? NotFound() : Ok(ToDto(alert));
    }

    [HttpPost("mark-all-read")]
    public async Task<ActionResult<object>> MarkAllReadAsync(
        [FromQuery] Guid portId,
        [FromBody] MarkAlertReadRequest? request,
        CancellationToken cancellationToken)
    {
        var count = await _alertService.MarkAllReadAsync(portId, request?.UserId, cancellationToken);
        return Ok(new { markedRead = count });
    }

    private static AlertDto ToDto(Alert alert)
        => new(
            alert.Id,
            alert.PortId,
            alert.AlertType,
            alert.Severity,
            alert.Title,
            alert.Message,
            alert.Metadata,
            alert.RelatedSopRuleId,
            alert.RelatedAssessmentId,
            alert.CreatedAt,
            alert.ReadAt,
            alert.ReadByUserId,
            alert.IsSimulation);

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
