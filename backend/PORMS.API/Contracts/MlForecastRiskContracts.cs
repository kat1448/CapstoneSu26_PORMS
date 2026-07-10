namespace PORMS.API.Contracts;

public sealed class ForecastRiskAnalysisRequest
{
    public string PortCode { get; set; } = "DNTSA";
    public IReadOnlyList<ForecastRiskAnalysisItemRequest> Items { get; set; } = [];
}

public sealed class ForecastRiskAnalysisItemRequest
{
    public DateTimeOffset PlannedAt { get; set; }
    public string RuleRiskLevel { get; set; } = "LOW";
    public string WindRiskLevel { get; set; } = "LOW";
    public string RainRiskLevel { get; set; } = "LOW";
    public string VisibilityRiskLevel { get; set; } = "LOW";
    public double? WindSpeedMs { get; set; }
    public double? RainfallMm { get; set; }
    public double? VisibilityKm { get; set; }
    public double? HumidityPct { get; set; }
    public double? PressureHpa { get; set; }
    public double? TemperatureC { get; set; }
}

public sealed class ForecastRiskAnalysisResponse
{
    public required string PortCode { get; init; }
    public required string ModelVersion { get; init; }
    public required IReadOnlyList<ForecastRiskAnalysisItemResponse> Items { get; init; }
    public OperationPlanAnalysisResponse? LlmPlanAnalysis { get; init; }
}

public sealed class ForecastRiskAnalysisItemResponse
{
    public DateTimeOffset PlannedAt { get; init; }
    public required string RuleRiskLevel { get; init; }
    public int PcaRiskScore { get; init; }
    public int ClusterId { get; init; }
    public required string ClusterLabel { get; init; }
    public required string MlRecommendation { get; init; }
    public required IReadOnlyList<string> DominantFactors { get; init; }
}

public sealed class OperationPlanAnalysisResponse
{
    public required string PortCode { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public bool IsConfigured { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<OperationPlanAnalysisItemResponse> Items { get; init; }
}

public sealed class OperationPlanAnalysisItemResponse
{
    public DateTimeOffset PlannedAt { get; init; }
    public required string OperationMode { get; init; }
    public required string PlanChange { get; init; }
    public required string Reason { get; init; }
    public required IReadOnlyList<string> RecommendedActions { get; init; }
    public required IReadOnlyList<string> AffectedOperations { get; init; }
}
