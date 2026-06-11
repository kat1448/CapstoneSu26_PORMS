using PORMS.Application.DTOs.Risk;
using PORMS.Application.DTOs.Weather;
using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Ports;

public sealed record PortStatusDto(
    Guid PortId,
    string PortCode,
    string PortName,
    OperationMode CurrentMode,
    RiskLevel CurrentRiskLevel,
    WeatherReadingDto? LatestWeather,
    RiskAssessmentDto? LatestRisk,
    IReadOnlyList<PortStatusZoneDto> Zones,
    int UnreadAlertCount,
    DateTimeOffset? LastWeatherAt,
    bool IsStale);

public sealed record PortStatusZoneDto(
    Guid ZoneId,
    string ZoneName,
    ZoneType ZoneType,
    RiskLevel CurrentRiskLevel,
    short DisplayOrder);

public sealed record WeatherFetchHealthDto(
    Guid PortId,
    DateTimeOffset? LastSuccessfulFetchAt,
    DateTimeOffset? LastAttemptAt,
    string? LastStatus,
    int? LastHttpStatusCode,
    string? LastErrorMessage,
    bool IsHealthy,
    bool IsStale);
