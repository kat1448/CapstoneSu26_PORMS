using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Ports;
using PORMS.Application.DTOs.Risk;
using PORMS.Application.DTOs.Weather;
using PORMS.Domain.Entities;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/ports")]
public sealed class PortController : ControllerBase
{
    private static readonly TimeSpan WeatherStaleAfter = TimeSpan.FromMinutes(30);
    private readonly IApplicationDbContext _dbContext;

    public PortController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    [ProducesResponseType<IReadOnlyList<PortStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortStatusDto>>> GetAllStatusAsync(
        CancellationToken cancellationToken)
    {
        var portIds = await _dbContext.Ports
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var statuses = new List<PortStatusDto>();
        foreach (var portId in portIds)
        {
            statuses.Add(await BuildStatusAsync(portId, cancellationToken));
        }

        return Ok(statuses);
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType<PortStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortStatusDto>> GetStatusAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Ports.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        return Ok(await BuildStatusAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}/fetch-health")]
    [ProducesResponseType<WeatherFetchHealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeatherFetchHealthDto>> GetFetchHealthAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Ports.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var lastJob = await _dbContext.WeatherFetchJobs
            .AsNoTracking()
            .Where(x => x.PortId == id)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastSuccess = await _dbContext.WeatherFetchJobs
            .AsNoTracking()
            .Where(x => x.PortId == id && x.Status == "SUCCESS")
            .OrderByDescending(x => x.CompletedAt ?? x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastSuccessfulAt = lastSuccess?.CompletedAt ?? lastSuccess?.StartedAt;
        var isStale = lastSuccessfulAt is null ||
            DateTimeOffset.UtcNow - lastSuccessfulAt.Value > WeatherStaleAfter;

        return Ok(new WeatherFetchHealthDto(
            id,
            lastSuccessfulAt,
            lastJob?.StartedAt,
            lastJob?.Status,
            lastJob?.HttpStatusCode,
            lastJob?.ErrorMessage,
            lastSuccess is not null && !isStale,
            isStale));
    }

    private async Task<PortStatusDto> BuildStatusAsync(Guid portId, CancellationToken cancellationToken)
    {
        var port = await _dbContext.Ports
            .AsNoTracking()
            .FirstAsync(x => x.Id == portId, cancellationToken);

        var latestWeather = await _dbContext.WeatherReadings
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation)
            .OrderByDescending(x => x.ObservedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestRisk = await _dbContext.RiskAssessments
            .AsNoTracking()
            .Where(x => x.PortId == portId && !x.IsSimulation)
            .OrderByDescending(x => x.EvaluatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var zones = await _dbContext.Zones
            .AsNoTracking()
            .Where(x => x.PortId == portId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new PortStatusZoneDto(
                x.Id,
                x.Name,
                x.ZoneType,
                x.CurrentRiskLevel,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);

        var unreadAlertCount = await _dbContext.Alerts
            .AsNoTracking()
            .CountAsync(x => x.PortId == portId && x.ReadAt == null && !x.IsSimulation, cancellationToken);

        var lastWeatherAt = latestWeather?.ObservedAt;
        var isStale = lastWeatherAt is null ||
            DateTimeOffset.UtcNow - lastWeatherAt.Value > WeatherStaleAfter;

        return new PortStatusDto(
            port.Id,
            port.Code,
            port.Name,
            port.CurrentMode,
            port.CurrentRiskLevel,
            latestWeather is null ? null : ToWeatherDto(latestWeather),
            latestRisk is null ? null : ToRiskDto(latestRisk),
            zones,
            unreadAlertCount,
            lastWeatherAt,
            isStale);
    }

    private static WeatherReadingDto ToWeatherDto(WeatherReading reading)
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

    private static RiskAssessmentDto ToRiskDto(RiskAssessment assessment)
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
}
