namespace PORMS.API.Contracts;

public sealed class DashboardSummaryResponse
{
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public string CurrentRiskLevel { get; set; } = string.Empty;
    public string CurrentOperationMode { get; set; } = string.Empty;
    public decimal? WindSpeedMs { get; set; }
    public short? BeaufortNumber { get; set; }
    public decimal? Rainfall1hMm { get; set; }
    public decimal? VisibilityKm { get; set; }
    public long ActiveAlertCount { get; set; }
}
