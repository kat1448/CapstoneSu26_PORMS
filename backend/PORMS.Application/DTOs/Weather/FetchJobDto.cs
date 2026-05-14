namespace PORMS.Application.DTOs.Weather;

public sealed record FetchJobDto(
    Guid Id,
    Guid PortId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? ResponseTimeMs,
    int? HttpStatusCode,
    string? ErrorMessage,
    Guid? CreatedReadingId,
    string? PrefectFlowRunId);
