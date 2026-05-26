namespace PORMS.Domain.Entities;

public class WeatherReading
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public Port? Port { get; set; }

    public decimal WindSpeedMs { get; set; }
    public int BeaufortNumber { get; set; }
    public int? WindDirectionDeg { get; set; }
    public decimal? WindGustMs { get; set; }

    public decimal? Rainfall1hMm { get; set; }
    public decimal? Rainfall3hMm { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? HumidityPct { get; set; }
    public decimal? VisibilityKm { get; set; }
    public decimal? PressureHpa { get; set; }

    public int? OpenWeatherCode { get; set; }
    public string? OpenWeatherDescription { get; set; }
    public string? OpenWeatherIcon { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string DataSource { get; set; } = "OPENWEATHER_API";
    public string? RawPayload { get; set; }
    public bool IsSimulation { get; set; }
}
