using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Risk;
using PORMS.Application.Services.Risk;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;
    private readonly IRiskThresholdService _thresholdService;

    public RiskController(
        IApplicationDbContext dbContext,
        IRiskThresholdService thresholdService)
    {
        _dbContext = dbContext;
        _thresholdService = thresholdService;
    }

    [HttpGet("current")]
    [ProducesResponseType<RiskAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<RiskAssessmentDto>> GetCurrentAsync(
        [FromQuery] Guid portId,
        CancellationToken cancellationToken)
    {
        var assessment = await _dbContext.RiskAssessments
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation)
            .OrderByDescending(x => x.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return assessment is null ? NoContent() : Ok(ToDto(assessment));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] Guid portId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool changedOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            return BadRequest("Query parameter 'from' must be earlier than or equal to 'to'.");
        }

        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);

        var query = _dbContext.RiskAssessments
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation);

        if (from.HasValue)
        {
            query = query.Where(x => x.EvaluatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.EvaluatedAt <= to.Value);
        }

        if (changedOnly)
        {
            query = query.Where(x => x.LevelChanged);
        }

        var total = await query.CountAsync(cancellationToken);
        var assessments = await query
            .OrderByDescending(x => x.EvaluatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = assessments,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = GetTotalPages(total, pageSize)
            }
        });
    }

    [HttpGet("assessments/{id:guid}/details")]
    [ProducesResponseType<IReadOnlyList<RiskAssessmentDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RiskAssessmentDetailDto>>> GetAssessmentDetailsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var details = await _dbContext.RiskAssessmentDetails
            .AsNoTracking()
            .Where(x => x.AssessmentId == id)
            .OrderBy(x => x.Factor)
            .Select(x => new RiskAssessmentDetailDto(
                x.Factor,
                x.RawValue,
                x.BeaufortNumber,
                x.RiskLevel,
                x.Unit,
                x.ThresholdApplied))
            .ToListAsync(cancellationToken);

        return Ok(details);
    }

    [HttpGet("thresholds")]
    [ProducesResponseType<IReadOnlyList<RiskThresholdDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RiskThresholdDto>>> GetThresholdsAsync(
        CancellationToken cancellationToken)
    {
        var thresholds = await _thresholdService.GetGlobalThresholdsAsync(cancellationToken);
        return Ok(thresholds.Select(ToDto).ToList());
    }

    [HttpGet("beaufort-reference")]
    [ProducesResponseType<IReadOnlyList<BeaufortReferenceDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<BeaufortReferenceDto>> GetBeaufortReference()
        => Ok(BeaufortReference);

    [HttpPut("thresholds/{id:guid}")]
    [ProducesResponseType<RiskThresholdDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskThresholdDto>> UpdateThresholdAsync(
        Guid id,
        [FromBody] UpdateRiskThresholdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var threshold = await _thresholdService.UpdateAsync(id, request, cancellationToken);
            return Ok(ToDto(threshold));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("thresholds/preview")]
    [ProducesResponseType<RiskThresholdPreviewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskThresholdPreviewResponse>> PreviewThresholdAsync(
        [FromBody] RiskThresholdPreviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _thresholdService.PreviewAsync(request, cancellationToken));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(exception.Message);
        }
    }

    private static RiskAssessmentDto ToDto(RiskAssessment assessment)
        => new(
            assessment.Id,
            assessment.PortId,
            assessment.WeatherReadingId,
            assessment.FinalRiskLevel,
            assessment.WindRiskLevel,
            assessment.RainRiskLevel,
            assessment.VisibilityRiskLevel,
            assessment.PreviousRiskLevel,
            assessment.LevelChanged,
            assessment.BeaufortNumber,
            assessment.Rainfall1hMm,
            assessment.VisibilityKm,
            assessment.AssessmentSummary,
            assessment.EvaluatedAt,
            assessment.IsSimulation);

    private static RiskThresholdDto ToDto(RiskThreshold threshold)
        => new(
            threshold.Id,
            null,
            threshold.Factor,
            threshold.RiskLevel,
            threshold.MinValue,
            threshold.MaxValue,
            threshold.Unit,
            threshold.Description,
            threshold.IsActive);

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

    private static readonly IReadOnlyList<BeaufortReferenceDto> BeaufortReference =
    [
        new(0, "Calm", 0.0m, 0.2m, RiskLevel.LOW),
        new(1, "Light air", 0.3m, 1.5m, RiskLevel.LOW),
        new(2, "Light breeze", 1.6m, 3.3m, RiskLevel.LOW),
        new(3, "Gentle breeze", 3.4m, 5.4m, RiskLevel.LOW),
        new(4, "Moderate breeze", 5.5m, 7.9m, RiskLevel.LOW),
        new(5, "Fresh breeze", 8.0m, 10.7m, RiskLevel.LOW),
        new(6, "Strong breeze", 10.8m, 13.8m, RiskLevel.MEDIUM),
        new(7, "Near gale", 13.9m, 17.1m, RiskLevel.MEDIUM),
        new(8, "Gale", 17.2m, 20.7m, RiskLevel.HIGH),
        new(9, "Strong gale", 20.8m, 24.4m, RiskLevel.HIGH),
        new(10, "Storm", 24.5m, 28.4m, RiskLevel.CRITICAL),
        new(11, "Violent storm", 28.5m, 32.6m, RiskLevel.CRITICAL),
        new(12, "Hurricane force", 32.7m, null, RiskLevel.CRITICAL)
    ];
}
