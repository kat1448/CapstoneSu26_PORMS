using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Tasks;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TaskController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;

    public TaskController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasksAsync(
        [FromQuery] Guid? portId,
        [FromQuery] Guid? zoneId,
        [FromQuery] bool includeSimulation = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.TaskLogs.AsNoTracking();
        if (portId.HasValue)
        {
            query = query.Where(x => x.PortId == portId.Value);
        }

        if (zoneId.HasValue)
        {
            query = query.Where(x => x.ZoneId == zoneId.Value);
        }

        if (!includeSimulation)
        {
            query = query.Where(x => !x.IsSimulation);
        }

        var total = await query.CountAsync(cancellationToken);
        var tasks = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TaskLogDto(
                x.Id,
                x.PortId,
                x.ZoneId,
                x.TriggeredByRuleId,
                x.TriggeredByAssessmentId,
                x.ActionType,
                x.ActionDescription,
                x.RiskLevelAtCreation,
                x.CreatedAt,
                x.IsSimulation))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = tasks,
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
