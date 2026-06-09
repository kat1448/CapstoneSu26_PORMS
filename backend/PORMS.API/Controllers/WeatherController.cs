using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Weather;
using PORMS.Application.Services.Risk;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using PORMS.Infrastructure.Weather;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private readonly IApplicationDbContext _dbContext;
    private readonly IRiskEngine _riskEngine;

    public WeatherController(IApplicationDbContext dbContext, IRiskEngine riskEngine)
    {
        _dbContext = dbContext;
        _riskEngine = riskEngine;
    }

    [HttpGet("latest")]
    [ProducesResponseType<WeatherReadingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<WeatherReadingDto>> GetLatestAsync(
        [FromQuery] Guid portId,
        CancellationToken cancellationToken)
    {
        var reading = await _dbContext.WeatherReadings
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation)
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return reading is null ? NoContent() : Ok(ToDto(reading));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] Guid portId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool includeSimulation = false,
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

        var query = _dbContext.WeatherReadings
            .AsNoTracking()
            .Where(x => x.PortId == portId);

        if (!includeSimulation)
        {
            query = query.Where(x => !x.IsSimulation);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.ObservedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.ObservedAt <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var readings = await query
            .OrderByDescending(x => x.ObservedAt)
            .ThenByDescending(x => x.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = readings,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = GetTotalPages(total, pageSize)
            }
        });
    }

    [HttpPost("manual-input")]
    [ProducesResponseType<WeatherReadingDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<WeatherReadingDto>> CreateManualInputAsync(
        [FromBody] ManualWeatherInputRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PortId == Guid.Empty ||
            request.WindSpeedMs < 0 ||
            request.Rainfall1hMm < 0 ||
            request.VisibilityKm < 0 ||
            request.HumidityPct is < 0 or > 100)
        {
            return BadRequest("Manual weather input has invalid port, wind, rain, visibility, or humidity values.");
        }

        var portExists = await _dbContext.Ports
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.PortId && x.IsActive, cancellationToken);

        if (!portExists)
        {
            return NotFound();
        }

        var reading = new WeatherReading
        {
            Id = Guid.NewGuid(),
            PortId = request.PortId,
            WindSpeedMs = request.WindSpeedMs,
            BeaufortNumber = OpenWeatherService.ConvertToBeaufort(request.WindSpeedMs),
            Rainfall1hMm = request.Rainfall1hMm,
            VisibilityKm = request.VisibilityKm,
            TemperatureC = request.TemperatureC,
            HumidityPct = request.HumidityPct,
            ObservedAt = request.ObservedAt,
            RecordedAt = DateTimeOffset.UtcNow,
            DataSource = "MANUAL",
            RawPayload = JsonSerializer.Serialize(new
            {
                request.Notes,
                enteredAt = DateTimeOffset.UtcNow
            }),
            IsSimulation = false
        };

        _dbContext.WeatherReadings.Add(reading);
        _dbContext.OperationEvents.Add(new OperationEvent
        {
            Id = Guid.NewGuid(),
            PortId = reading.PortId,
            EventType = OperationEventType.WEATHER_FETCHED,
            Payload = JsonSerializer.Serialize(new
            {
                readingId = reading.Id,
                source = reading.DataSource,
                reading.WindSpeedMs,
                reading.BeaufortNumber,
                reading.Rainfall1hMm,
                reading.VisibilityKm,
                reading.ObservedAt
            }),
            Summary = $"Manual weather input recorded. Beaufort {reading.BeaufortNumber}.",
            OccurredAt = reading.RecordedAt,
            IsSimulation = false
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _riskEngine.EvaluateRiskAsync(reading, cancellationToken);

        return Created($"/api/weather/latest?portId={reading.PortId}", ToDto(reading));
    }

    [HttpGet("fetch-jobs")]
    public async Task<IActionResult> GetFetchJobsAsync(
        [FromQuery] Guid portId,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
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

        var query = _dbContext.WeatherFetchJobs
            .AsNoTracking()
            .Where(x => x.PortId == portId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.ToUpperInvariant());
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.StartedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.StartedAt <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var fetchJobs = await query
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FetchJobDto(
                x.Id,
                x.PortId,
                x.SourceId,
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.ResponseTimeMs,
                x.HttpStatusCode,
                x.ErrorMessage,
                x.CreatedReadingId,
                x.PrefectFlowRunId))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = fetchJobs,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = GetTotalPages(total, pageSize)
            }
        });
    }

    private static WeatherReadingDto ToDto(WeatherReading reading)
        => new(
            reading.Id,
            reading.PortId,
            reading.WindSpeedMs,
            reading.BeaufortNumber,
            reading.WindDirectionDeg,
            reading.WindGustMs,
            reading.Rainfall1hMm,
            reading.Rainfall3hMm,
            reading.VisibilityKm,
            reading.TemperatureC,
            reading.HumidityPct,
            reading.PressureHpa,
            reading.OpenWeatherCode,
            reading.OpenWeatherDescription,
            reading.OpenWeatherIcon,
            reading.ObservedAt,
            reading.RecordedAt,
            reading.DataSource,
            reading.IsSimulation);

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static int GetTotalPages(int total, int pageSize)
        => total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
}
