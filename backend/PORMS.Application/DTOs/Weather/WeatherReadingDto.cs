namespace PORMS.Application.DTOs.Weather;

public sealed record WeatherReadingDto(
    Guid Id,
    Guid PortId,
    decimal WindSpeedMs,
    int BeaufortNumber,
    int? WindDirectionDeg,
    decimal? WindGustMs,
    decimal? Rainfall1hMm,
    decimal? Rainfall3hMm,
    decimal? VisibilityKm,
    decimal? TemperatureC,
    int? HumidityPct,
    decimal? PressureHpa,
    int? OpenWeatherCode,
    string? OpenWeatherDescription,
    string? OpenWeatherIcon,
    DateTimeOffset ObservedAt,
    DateTimeOffset RecordedAt,
    string DataSource,
    bool IsSimulation);
