namespace PORMS.API.Contracts;

public sealed class ForecastEvaluationResponse
{
    public required ForecastEvaluationSummaryResponse Summary { get; init; }
    public required IReadOnlyList<ForecastEvaluationRowResponse> Rows { get; init; }
    public bool IsDemonstration { get; init; }
    public string? DataNotice { get; init; }
}

public sealed class ForecastEvaluationSummaryResponse
{
    public int TotalForecastPoints { get; init; }
    public int EligiblePastPoints { get; init; }
    public int MatchedActualPoints { get; init; }
    public decimal MatchRatePct { get; init; }
    public decimal? ConfidencePct { get; init; }
    public required string ConfidenceLevel { get; init; }
    public decimal RiskMatchRatePct { get; init; }
    public int ConsecutiveMismatchCount { get; init; }
    public int DangerousUnderestimateCount { get; init; }
    public bool InterventionRequired { get; init; }
    public required string InterventionMessage { get; init; }
    public required IReadOnlyList<string> RecommendedActions { get; init; }
    public decimal? AvgWindMae { get; init; }
    public decimal? AvgRainMae { get; init; }
    public decimal? AvgVisibilityMae { get; init; }
    public decimal? AvgRiskScoreError { get; init; }
    public IReadOnlyList<ForecastHorizonConfidenceResponse> HorizonConfidence { get; init; } = Array.Empty<ForecastHorizonConfidenceResponse>();
}

public sealed class ForecastHorizonConfidenceResponse
{
    public int HorizonDay { get; init; }
    public int SampleCount { get; init; }
    public decimal? ConfidencePct { get; init; }
    public required string ConfidenceLevel { get; init; }
    public decimal? AvgWindMae { get; init; }
    public decimal? AvgRainMae { get; init; }
    public decimal? AvgVisibilityMae { get; init; }
}

public sealed class ForecastEvaluationRowResponse
{
    public required string DatasetName { get; init; }
    public required string PortCode { get; init; }
    public required string PortName { get; init; }
    public int SnapshotNumber { get; init; }
    public DateTimeOffset PlannedAt { get; init; }
    public DateTimeOffset? ActualObservedAt { get; init; }
    public decimal ForecastWindSpeedMs { get; init; }
    public decimal? ActualWindSpeedMs { get; init; }
    public decimal? WindAbsError { get; init; }
    public decimal ForecastRainfallMm { get; init; }
    public decimal? ActualRainfallMm { get; init; }
    public decimal? RainAbsError { get; init; }
    public decimal? ForecastVisibilityKm { get; init; }
    public decimal? ActualVisibilityKm { get; init; }
    public decimal? VisibilityAbsError { get; init; }
    public required string ForecastRiskLevel { get; init; }
    public string? ActualRiskLevel { get; init; }
    public int? RiskScoreError { get; init; }
    public string? ActualDataSource { get; init; }
    public required string Status { get; init; }
}
