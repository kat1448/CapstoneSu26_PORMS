using Npgsql;
using NpgsqlTypes;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class ReportRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public ReportRepository(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<ReportRowReadModel>> GetRowsAsync(
        string reportType, string? portCode, string? zoneName, DateTimeOffset? from, DateTimeOffset? to,
        string? riskLevel, Guid userId, Guid? portId, string role, CancellationToken cancellationToken)
    {
        var normalized = reportType.Trim().ToUpperInvariant();
        if (normalized is not ("ALERTS" or "TASKS" or "EVENTS"))
            throw new ArgumentException("Loại báo cáo chưa được hỗ trợ.", nameof(reportType));

        var sql = normalized switch
        {
            "ALERTS" => """
                SELECT a.created_at, p.code, p.name, z.name, a.severity::text, a.title, a.message,
                       CASE WHEN a.alert_type = 'SIMULATION' THEN 'Mô phỏng vận hành' ELSE 'Vận hành thực tế' END,
                       CASE WHEN ar.acknowledged_at IS NULL THEN 'Chưa xác nhận' ELSE 'Đã xác nhận' END
                FROM operational.alerts a
                JOIN operational.ports p ON p.id = a.port_id
                LEFT JOIN operational.zones z ON z.id = a.zone_id
                LEFT JOIN operational.alert_receipts ar ON ar.alert_id = a.id AND ar.user_id = @userId
                WHERE (@isAdmin OR p.id = @portId)
                  AND (@portCode IS NULL OR p.code = @portCode)
                  AND (@zoneName IS NULL OR z.name = @zoneName)
                  AND (@fromDate IS NULL OR a.created_at >= @fromDate)
                  AND (@toDate IS NULL OR a.created_at <= @toDate)
                  AND (@riskLevel IS NULL OR a.severity::text = @riskLevel)
                ORDER BY a.created_at DESC LIMIT 1000;
                """,
            "TASKS" => """
                SELECT t.created_at, p.code, p.name, z.name, t.priority::text, t.title, t.description,
                       COALESCE(u.full_name, 'Chưa phân công'), t.status::text
                FROM operational.tasks t
                JOIN operational.ports p ON p.id = t.port_id
                LEFT JOIN operational.zones z ON z.id = t.zone_id
                LEFT JOIN operational.users u ON u.id = t.assigned_user_id
                WHERE (@isAdmin OR p.id = @portId)
                  AND (@portCode IS NULL OR p.code = @portCode)
                  AND (@zoneName IS NULL OR z.name = @zoneName)
                  AND (@fromDate IS NULL OR t.created_at >= @fromDate)
                  AND (@toDate IS NULL OR t.created_at <= @toDate)
                  AND (@riskLevel IS NULL OR t.priority::text = @riskLevel)
                ORDER BY t.created_at DESC LIMIT 1000;
                """,
            _ => """
                SELECT e.occurred_at, p.code, p.name, z.name, NULL::text, e.event_type, e.summary,
                       COALESCE(u.full_name, 'Hệ thống PORMS'), 'Nhật ký vận hành'
                FROM operational.operation_events e
                LEFT JOIN operational.ports p ON p.id = e.port_id
                LEFT JOIN operational.zones z ON z.id = e.zone_id
                LEFT JOIN operational.users u ON u.id = e.actor_user_id
                WHERE (@isAdmin OR p.id = @portId)
                  AND (@portCode IS NULL OR p.code = @portCode)
                  AND (@zoneName IS NULL OR z.name = @zoneName)
                  AND (@fromDate IS NULL OR e.occurred_at >= @fromDate)
                  AND (@toDate IS NULL OR e.occurred_at <= @toDate)
                ORDER BY e.occurred_at DESC LIMIT 1000;
                """
        };

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        command.Parameters.AddWithValue("portId", portId ?? Guid.Empty);
        AddText(command, "portCode", portCode);
        AddText(command, "zoneName", zoneName);
        AddText(command, "riskLevel", riskLevel);
        AddTimestamp(command, "fromDate", from);
        AddTimestamp(command, "toDate", to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ReportRowReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ReportRowReadModel(
                reader.GetFieldValue<DateTimeOffset>(0),
                reader.IsDBNull(1) ? "-" : reader.GetString(1),
                reader.IsDBNull(2) ? "-" : reader.GetString(2),
                reader.IsDBNull(3) ? "Toàn cảng" : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? "-" : reader.GetString(5),
                reader.IsDBNull(6) ? "-" : reader.GetString(6),
                reader.IsDBNull(7) ? "-" : reader.GetString(7),
                reader.IsDBNull(8) ? "-" : reader.GetString(8)));
        }
        return rows;
    }

    private static void AddText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static void AddTimestamp(NpgsqlCommand command, string name, DateTimeOffset? value) =>
        command.Parameters.Add(name, NpgsqlDbType.TimestampTz).Value = value.HasValue ? value.Value : DBNull.Value;
}

public sealed record ReportRowReadModel(
    DateTimeOffset OccurredAt, string PortCode, string PortName, string ZoneName, string? RiskLevel,
    string Subject, string Description, string Owner, string Status);
