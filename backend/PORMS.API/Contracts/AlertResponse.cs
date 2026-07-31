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
    public short? BeaufortNumber { get; set; }
    public decimal? WindSpeedMs { get; set; }
    public decimal? Rainfall1hMm { get; set; }
    public decimal? VisibilityKm { get; set; }
    public long RecipientCount { get; set; }
    public long ReadCount { get; set; }
    public long AcknowledgedCount { get; set; }
    public bool Read { get; set; }
    public bool Acknowledged { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string Status { get; set; } = "NEW";
}
