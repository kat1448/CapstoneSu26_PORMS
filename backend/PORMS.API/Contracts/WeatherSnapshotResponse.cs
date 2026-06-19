namespace PORMS.API.Contracts;

public sealed class WeatherSnapshotResponse
{
    public decimal WindSpeedMs { get; set; }
    public decimal Rainfall1hMm { get; set; }
    public decimal VisibilityKm { get; set; }
    public decimal TemperatureC { get; set; }
    public short HumidityPct { get; set; }
}
