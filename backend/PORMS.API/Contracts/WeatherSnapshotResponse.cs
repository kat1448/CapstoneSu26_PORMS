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
}
