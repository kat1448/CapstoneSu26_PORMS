using PORMS.Domain.Enums;

namespace PORMS.Application.DTOs.Alerts;

public sealed record AlertStatsDto(
    Guid? PortId,
    DateOnly Date,
    int TotalToday,
    int Unread,
    int CriticalToday,
    int ReadToday,
    double? AverageResponseMinutes,
    IReadOnlyDictionary<AlertSeverity, int> BySeverity);
