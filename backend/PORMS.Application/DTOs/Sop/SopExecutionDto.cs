namespace PORMS.Application.DTOs.Sop;

public sealed record SopExecutionDto(
    Guid Id,
    Guid RuleId,
    Guid? RiskAssessmentId,
    Guid PortId,
    Guid? ZoneId,
    DateTimeOffset ExecutedAt,
    string ExecutionResult,
    string? SkipReason,
    int DurationMs,
    bool IsSimulation);
