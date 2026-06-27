namespace PORMS.API.Contracts;

public sealed class RiskConfigResponse
{
    public required IReadOnlyList<RiskThresholdResponse> Thresholds { get; init; }
    public required IReadOnlyList<ZoneThresholdOverrideResponse> ZoneOverrides { get; init; }
    public required IReadOnlyList<RiskConfigZoneResponse> Zones { get; init; }
}

public sealed class RiskThresholdResponse
{
    public required Guid Id { get; init; }
    public required string Factor { get; init; }
    public required string RiskLevel { get; init; }
    public required string ComparisonOperator { get; init; }
    public required decimal ThresholdValue { get; init; }
    public required string Unit { get; init; }
    public string? Description { get; init; }
    public required int Version { get; init; }
    public required bool IsEnabled { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ZoneThresholdOverrideResponse
{
    public required Guid Id { get; init; }
    public required Guid ZoneId { get; init; }
    public required string ZoneName { get; init; }
    public required string ZoneType { get; init; }
    public required string Factor { get; init; }
    public required string RiskLevel { get; init; }
    public required string ComparisonOperator { get; init; }
    public required decimal ThresholdValue { get; init; }
    public required string Unit { get; init; }
    public required bool IsEnabled { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class RiskConfigZoneResponse
{
    public required Guid ZoneId { get; init; }
    public required string ZoneName { get; init; }
    public required string ZoneType { get; init; }
    public required string PortName { get; init; }
}

public sealed class SaveRiskThresholdsRequest
{
    public string? ChangeReason { get; init; }
    public required IReadOnlyList<SaveRiskThresholdRequest> Thresholds { get; init; }
}

public sealed class SaveRiskThresholdRequest
{
    public required string Factor { get; init; }
    public required string RiskLevel { get; init; }
    public required string ComparisonOperator { get; init; }
    public required decimal ThresholdValue { get; init; }
    public required string Unit { get; init; }
    public string? Description { get; init; }
    public int Version { get; init; } = 1;
    public bool IsEnabled { get; init; } = true;
}

public sealed class SaveZoneThresholdOverridesRequest
{
    public string? ChangeReason { get; init; }
    public required IReadOnlyList<SaveZoneThresholdOverrideRequest> Overrides { get; init; }
}

public sealed class SaveZoneThresholdOverrideRequest
{
    public required string Factor { get; init; }
    public required string RiskLevel { get; init; }
    public required string ComparisonOperator { get; init; }
    public required decimal ThresholdValue { get; init; }
    public required string Unit { get; init; }
    public bool IsEnabled { get; init; } = true;
}
