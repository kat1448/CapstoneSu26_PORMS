using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class RiskThreshold
{
    public Guid Id { get; set; }
    public WeatherFactor Factor { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
