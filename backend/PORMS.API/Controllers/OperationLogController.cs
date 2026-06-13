using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/operation-events")]
[Route("api/operation-logs")]
public sealed class OperationLogController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;

    public OperationLogController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetEventsAsync(
        [FromQuery] Guid? portId,
        [FromQuery] OperationEventType? eventType,
        [FromQuery] bool includeSimulation = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.OperationEvents.AsNoTracking();
        if (portId.HasValue)
        {
            query = query.Where(x => x.PortId == portId.Value);
        }

        if (eventType.HasValue)
        {
            query = query.Where(x => x.EventType == eventType.Value);
        }

        if (!includeSimulation)
        {
            query = query.Where(x => !x.IsSimulation);
        }

        var total = await query.CountAsync(cancellationToken);
        var events = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.PortId,
                x.EventType,
                x.ActorUserId,
                x.Payload,
                x.Summary,
                x.OccurredAt,
                x.IsSimulation
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = events,
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
