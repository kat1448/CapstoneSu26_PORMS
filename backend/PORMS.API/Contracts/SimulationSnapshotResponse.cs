namespace PORMS.API.Contracts;

public sealed class SimulationSnapshotResponse
{
    public string Status { get; set; } = "IDLE";
    public string CurrentRiskLevel { get; set; } = "LOW";
    public string CurrentMode { get; set; } = "NORMAL";
    public decimal WindSpeedMs { get; set; }
    public short BeaufortNumber { get; set; }
    public decimal Rainfall1hMm { get; set; }
    public decimal VisibilityKm { get; set; }
    public decimal ProgressPercent { get; set; }
    public int GeneratedAlertCount { get; set; }
    public int ModeChangeCount { get; set; }
    public IReadOnlyList<SimulationFeedItemResponse> Feed { get; set; } = [];
}

public sealed class SimulationFeedItemResponse
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "LOW";
    public string HappenedAt { get; set; } = string.Empty;
}
