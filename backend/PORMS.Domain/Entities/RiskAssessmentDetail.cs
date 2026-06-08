using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class RiskAssessmentDetail
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public RiskAssessment? Assessment { get; set; }
    public WeatherFactor Factor { get; set; }
    public decimal RawValue { get; set; }
    public int? BeaufortNumber { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string ThresholdApplied { get; set; } = string.Empty;
}
