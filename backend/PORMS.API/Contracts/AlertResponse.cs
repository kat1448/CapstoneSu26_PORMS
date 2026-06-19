namespace PORMS.API.Contracts;

public sealed class AlertResponse
{
    public Guid AlertId { get; set; }
    public Guid PortId { get; set; }
    public string PortCode { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public long RecipientCount { get; set; }
    public long ReadCount { get; set; }
    public long AcknowledgedCount { get; set; }
}
