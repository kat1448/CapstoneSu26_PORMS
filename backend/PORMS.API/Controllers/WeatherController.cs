using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
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
            DataSource = weather.DataSource,
            DataPoints = weather.DataPoints.Select(point => new WeatherDataPointResponse
            {
                PortCode = point.PortCode,
                PortName = point.PortName,
                ZoneName = point.ZoneName,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                WindSpeedMs = point.WindSpeedMs,
                BeaufortNumber = point.BeaufortNumber,
                Rainfall1hMm = point.Rainfall1hMm,
                VisibilityKm = point.VisibilityKm,
                TemperatureC = point.TemperatureC,
                HumidityPct = point.HumidityPct,
                WeatherDescription = point.WeatherDescription,
                ObservedAt = point.ObservedAt,
                RecordedAt = point.RecordedAt,
                DataSource = point.DataSource
            }).ToList()
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh(
        [FromServices] OpenWeatherService openWeatherService,
        CancellationToken cancellationToken)
    {
        var result = await openWeatherService.RefreshActivePortsAsync(cancellationToken);
        return Ok(new { result.FetchedCount });
    }

    [HttpGet("forecast")]
    public async Task<ActionResult<OpenWeatherForecastResponse>> GetForecast(
        [FromQuery] string portCode,
        [FromQuery] int days,
        [FromServices] OpenWeatherService openWeatherService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portCode))
        {
            return BadRequest(new ErrorResponse { Error = "Port code is required." });
        }

        var requestedDays = days == 0 ? 5 : days;
        if (requestedDays is < 1 or > 5)
        {
            return BadRequest(new ErrorResponse { Error = "Forecast days must be between 1 and 5." });
        }

        try
        {
            var forecast = await openWeatherService.FetchDailyForecastAsync(portCode, requestedDays, cancellationToken);
            return Ok(new OpenWeatherForecastResponse
            {
                PortCode = forecast.PortCode,
                PortName = forecast.PortName,
                FetchedAt = forecast.FetchedAt,
                Days = forecast.Days.Select(day => new OpenWeatherForecastDayResponse
                {
                    Date = day.Date,
                    TemperatureDayC = day.TemperatureDayC,
                    TemperatureMinC = day.TemperatureMinC,
                    TemperatureMaxC = day.TemperatureMaxC,
                    VisibilityKm = day.VisibilityKm,
                    WindSpeedMs = day.WindSpeedMs,
                    WindGustMs = day.WindGustMs,
                    WindDirectionDeg = day.WindDirectionDeg,
                    RainMm = day.RainMm,
                    PopPct = day.PopPct,
                    HumidityPct = day.HumidityPct,
                    PressureHpa = day.PressureHpa,
                    WeatherCode = day.WeatherCode,
                    WeatherDescription = day.WeatherDescription,
                    Summary = day.Summary
                }).ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse { Error = $"OpenWeather forecast request failed: {ex.Message}" });
        }
    }
}
