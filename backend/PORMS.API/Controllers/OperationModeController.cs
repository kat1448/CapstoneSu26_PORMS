using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Mode;
using PORMS.Application.Services.Mode;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/ports/{portId:guid}/mode")]
public sealed class OperationModeController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;
    private readonly IOperationModeService _operationModeService;

    public OperationModeController(
        IApplicationDbContext dbContext,
        IOperationModeService operationModeService)
    {
        _dbContext = dbContext;
        _operationModeService = operationModeService;
    }

    [HttpGet("current")]
    [ProducesResponseType<OperationModeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationModeDto>> GetCurrentAsync(
        Guid portId,
        CancellationToken cancellationToken)
    {
        var port = await _dbContext.Ports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == portId, cancellationToken);
        if (port is null)
        {
            return NotFound();
        }

        var latestLog = await _operationModeService.GetLatestLogAsync(portId, cancellationToken);
        return Ok(new OperationModeDto(
            port.Id,
            port.CurrentMode,
            port.CurrentRiskLevel,
            latestLog?.ChangedAt,
            latestLog?.ChangeType,
            latestLog?.TriggeredBySopRuleId));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync(
        Guid portId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.OperationModeLogs
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation);

        var total = await query.CountAsync(cancellationToken);
        var logs = await query
            .OrderByDescending(x => x.ChangedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OperationModeLogDto(
                x.Id,
                x.PortId,
                x.PreviousMode,
                x.NewMode,
                x.TriggeredByRiskLevel,
                x.TriggeredBySopRuleId,
                x.OverriddenByUserId,
                x.OverrideReason,
                x.ChangeType,
                x.ChangedAt,
                x.IsSimulation))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = logs,
            pagination = new { page, pageSize, total, totalPages = GetTotalPages(total, pageSize) }
        });
    }

    [HttpPost("override")]
    [ProducesResponseType<OperationModeLogDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationModeLogDto>> OverrideAsync(
        Guid portId,
        [FromBody] OverrideModeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _operationModeService.OverrideModeAsync(
                portId,
                request.TargetMode,
                request.OverrideReason,
                request.UserId,
                cancellationToken);

            return Ok(new OperationModeLogDto(
                log.Id,
                log.PortId,
                log.PreviousMode,
                log.NewMode,
                log.TriggeredByRiskLevel,
                log.TriggeredBySopRuleId,
                log.OverriddenByUserId,
                log.OverrideReason,
                log.ChangeType,
                log.ChangedAt,
                log.IsSimulation));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
