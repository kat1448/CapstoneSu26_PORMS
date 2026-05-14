using PORMS.Domain.Enums;

namespace PORMS.Domain.Entities;

public class Port
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsActive { get; set; } = true;
    public OperationMode CurrentMode { get; set; } = OperationMode.NORMAL;
    public RiskLevel CurrentRiskLevel { get; set; } = RiskLevel.LOW;
    public string? OpenWeatherStationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
