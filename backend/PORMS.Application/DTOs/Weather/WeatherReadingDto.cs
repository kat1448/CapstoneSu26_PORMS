namespace PORMS.Application.DTOs.Weather;

public sealed record WeatherReadingDto(
    Guid Id,
    Guid PortId,
    Guid? FetchJobId,
    decimal WindSpeedMs,
    int BeaufortNumber,
    decimal Rainfall1hMm,
    decimal? VisibilityKm,
    decimal? TemperatureC,
    int? HumidityPct,
    DateTimeOffset ObservedAt,
    DateTimeOffset CreatedAt,
    bool IsSimulation);
