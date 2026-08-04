namespace PORMS.API.Contracts
{
    /// Dữ liệu Prefect ETL gửi sau khi lưu weather reading thành công
    public sealed class TriggerRiskEngineRequest
    {
        public required Guid PortId { get; init; }
        public required Guid WeatherReadingId { get; init; }
    }

    /// Kết quả đánh giá rủi ro của một weather reading
    /// Created = false nghĩa là reading đã được xử lý trước đó
    public sealed class TriggerRiskEngineResponse
    {
        public required Guid RiskAssessmentId { get; init; }
        public required Guid PortId { get; init; }
        public required Guid WeatherReadingId { get; init; }
        public required bool Created { get; init; }
        public required string WindRiskLevel { get; init; }
        public required string RainRiskLevel { get; init; }
        public required string VisibilityRiskLevel { get; init; }
        public required string FinalRiskLevel { get; init; }
        public required string? PreviousRiskLevel { get; init; }
        public required bool LevelChanged { get; init; }
        public required string DominantFactor { get; init; }
        public required string Summary { get; init; }
    }
}
