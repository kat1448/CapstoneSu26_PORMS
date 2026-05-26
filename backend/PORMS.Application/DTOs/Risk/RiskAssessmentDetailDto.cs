using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Risk;

public sealed record RiskAssessmentDetailDto(
    WeatherFactor Factor,
    decimal RawValue,
    int? BeaufortNumber,
    RiskLevel RiskLevel,
    string Unit,
    string ThresholdApplied);
