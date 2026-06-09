namespace PORMS.Application.DTOs.Weather;

public sealed record ManualWeatherInputRequest(
    Guid PortId,
    decimal WindSpeedMs,
    decimal Rainfall1hMm,
    decimal? VisibilityKm,
    decimal? TemperatureC,
    int? HumidityPct,
    DateTimeOffset ObservedAt,
    string? Notes);
