using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class RiskAssessment
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public Port? Port { get; set; }
    public Guid WeatherReadingId { get; set; }
    public WeatherReading? WeatherReading { get; set; }
    public ICollection<RiskAssessmentDetail> Details { get; set; } = new List<RiskAssessmentDetail>();

    public RiskLevel FinalRiskLevel { get; set; }
    public RiskLevel WindRiskLevel { get; set; }
    public RiskLevel RainRiskLevel { get; set; }
    public RiskLevel? VisibilityRiskLevel { get; set; }

    public int BeaufortNumber { get; set; }
    public decimal? Rainfall1hMm { get; set; }
    public decimal? VisibilityKm { get; set; }

    public RiskLevel? PreviousRiskLevel { get; set; }
    public bool LevelChanged { get; set; }
    public string? AssessmentSummary { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
    public bool IsSimulation { get; set; }
}
