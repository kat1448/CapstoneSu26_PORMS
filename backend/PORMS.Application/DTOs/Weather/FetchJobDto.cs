namespace PORMS.Application.DTOs.Weather;

public sealed record FetchJobDto(
    Guid Id,
    Guid PortId,
    Guid? SourceId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? ResponseTimeMs,
    int? HttpStatusCode,
    string? ErrorMessage,
    Guid? CreatedReadingId,
    string? PrefectFlowRunId);
