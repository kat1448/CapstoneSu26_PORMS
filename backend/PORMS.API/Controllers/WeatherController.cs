using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<WeatherSnapshotResponse>> GetCurrent(
        [FromServices] WeatherRepository repository,
        CancellationToken cancellationToken)
    {
        var weather = await repository.GetCurrentAsync(cancellationToken);

        return Ok(new WeatherSnapshotResponse
        {
            WindSpeedMs = weather.WindSpeedMs,
            BeaufortNumber = weather.BeaufortNumber,
            WindDirectionDeg = weather.WindDirectionDeg,
            WindGustMs = weather.WindGustMs,
            Rainfall1hMm = weather.Rainfall1hMm,
            VisibilityKm = weather.VisibilityKm,
            TemperatureC = weather.TemperatureC,
            HumidityPct = weather.HumidityPct,
            PressureHpa = weather.PressureHpa,
            WeatherCode = weather.WeatherCode,
            WeatherDescription = weather.WeatherDescription,
            ObservedAt = weather.ObservedAt,
            RecordedAt = weather.RecordedAt,
            DataSource = weather.DataSource
        });
    }
}
