namespace PORMS.Application.DTOs.Simulation;

public sealed record SimulationWeatherSnapshotDto(
    decimal WindSpeedMs,
    decimal Rainfall1hMm,
    decimal VisibilityKm,
    decimal? TemperatureC,
    int? HumidityPct,
    DateTimeOffset ObservedAt,
    int? WindDirectionDeg = null,
    decimal? WindGustMs = null,
    decimal? Rainfall3hMm = null,
    decimal? PressureHpa = null);
