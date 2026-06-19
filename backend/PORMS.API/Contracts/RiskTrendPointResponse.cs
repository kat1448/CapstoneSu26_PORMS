namespace PORMS.API.Contracts;

public sealed class RiskTrendPointResponse
{
    public string HourLabel { get; set; } = string.Empty;
    public short RiskScore { get; set; }
}
