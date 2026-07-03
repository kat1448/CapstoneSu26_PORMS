namespace PORMS.API.Contracts;

public sealed class WeatherSnapshotResponse
{
    public decimal WindSpeedMs { get; set; }
    public short BeaufortNumber { get; set; }
    public short? WindDirectionDeg { get; set; }
    public decimal? WindGustMs { get; set; }
    public decimal Rainfall1hMm { get; set; }
    public decimal VisibilityKm { get; set; }
    public decimal TemperatureC { get; set; }
    public short HumidityPct { get; set; }
    public decimal? PressureHpa { get; set; }
    public int? WeatherCode { get; set; }
    public string? WeatherDescription { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public IReadOnlyList<WeatherDataPointResponse> DataPoints { get; set; } = Array.Empty<WeatherDataPointResponse>();
}

public sealed class WeatherDataPointResponse
{
    public required string PortCode { get; init; }
    public required string PortName { get; init; }
    public string? ZoneName { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public required decimal WindSpeedMs { get; init; }
    public required short BeaufortNumber { get; init; }
    public required decimal Rainfall1hMm { get; init; }
    public required decimal VisibilityKm { get; init; }
    public required decimal TemperatureC { get; init; }
    public string? WeatherDescription { get; init; }
    public DateTimeOffset? ObservedAt { get; init; }
    public string DataSource { get; init; } = string.Empty;
}

public sealed class OpenWeatherForecastResponse
{
    public required string PortCode { get; init; }
    public required string PortName { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
    public required IReadOnlyList<OpenWeatherForecastDayResponse> Days { get; init; }
}

public sealed class OpenWeatherForecastDayResponse
{
    public required DateTimeOffset Date { get; init; }
    public required decimal TemperatureDayC { get; init; }
    public required decimal TemperatureMinC { get; init; }
    public required decimal TemperatureMaxC { get; init; }
    public required decimal WindSpeedMs { get; init; }
    public decimal? WindGustMs { get; init; }
    public short? WindDirectionDeg { get; init; }
    public required decimal RainMm { get; init; }
    public required short PopPct { get; init; }
    public required short HumidityPct { get; init; }
    public decimal? PressureHpa { get; init; }
    public int? WeatherCode { get; init; }
    public string? WeatherDescription { get; init; }
    public string? Summary { get; init; }
}
