namespace PORMS.API.Contracts;

public sealed class PortSummaryResponse
{
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public string CurrentRiskLevel { get; set; } = string.Empty;
    public string CurrentOperationMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public long ActiveAlertCount { get; set; }
    public string UpdatedAtLabel { get; set; } = string.Empty;
}
