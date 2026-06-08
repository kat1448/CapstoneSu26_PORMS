namespace PORMS.Domain.Entities;

public class WeatherFetchJob
{
    public Guid Id { get; set; }
    public Guid PortId { get; set; }
    public Port? Port { get; set; }
    public Guid? SourceId { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? ResponseTimeMs { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedReadingId { get; set; }
    public WeatherReading? CreatedReading { get; set; }
    public string? PrefectFlowRunId { get; set; }
}
