using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class AlertRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public AlertRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AlertReadModel>> GetAlertsAsync(
        Guid userId,
        string role,
        Guid? portId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH alert_feed AS (
                SELECT a.id,
                       a.port_id,
                       p.code AS port_code,
                       p.name AS port_name,
                       COALESCE(a.zone_id, ra.zone_id) AS zone_id,
                       z.name AS zone_name,
                       a.alert_type::text AS alert_type,
                       a.severity::text AS severity,
                       CASE
                           WHEN a.alert_type = 'SIMULATION' AND a.severity = 'CRITICAL'
                               THEN CONCAT('Cảnh báo nguy hiểm tại ', p.name)
                           WHEN a.alert_type = 'SIMULATION' AND a.severity = 'HIGH'
                               THEN CONCAT('Cảnh báo rủi ro cao tại ', p.name)
                           ELSE a.title
                       END AS title,
                       CASE
                           WHEN a.alert_type = 'SIMULATION' THEN CONCAT(
                               'Tại ', COALESCE(z.name, 'khu vực toàn cảng'), ' thuộc ', p.name,
                               ' đang có gió cấp ', COALESCE(w.beaufort_number::text, 'chưa xác định'),
                               COALESCE(', tốc độ ' || TRIM(TO_CHAR(w.wind_speed_ms, 'FM999990D0')) || ' m/s', ''),
                               COALESCE(', lượng mưa ' || TRIM(TO_CHAR(w.rainfall_1h_mm, 'FM999990D0')) || ' mm/giờ', ''),
                               COALESCE(' và tầm nhìn ' || TRIM(TO_CHAR(w.visibility_km, 'FM999990D0')) || ' km', ''),
                               '. Hệ thống đánh giá mức rủi ro ',
                               CASE a.severity
                                   WHEN 'CRITICAL' THEN 'rất cao, cần hành động ngay'
                                   WHEN 'HIGH' THEN 'cao, cần chủ động ứng phó'
                                   WHEN 'MEDIUM' THEN 'cần lưu ý'
                                   ELSE 'thấp'
                               END,
                               '. Theo quy trình ứng phó, ',
                               CASE a.severity
                                   WHEN 'CRITICAL' THEN 'đề nghị tạm dừng vận hành tại khu vực và thực hiện ngay nhiệm vụ khẩn cấp.'
                                   WHEN 'HIGH' THEN 'đề nghị hạn chế vận hành tại khu vực và thực hiện các nhiệm vụ được giao.'
                                   ELSE 'đề nghị tiếp tục theo dõi tình hình.'
                               END)
                           ELSE a.message
                       END AS message,
                       CASE
                           WHEN a.simulation_session_id IS NOT NULL AND a.created_at > NOW()
                               THEN COALESCE(s.created_at, s.started_at, a.created_at)
                           ELSE a.created_at
                       END AS display_created_at,
                       a.expires_at,
                       w.beaufort_number,
                       w.wind_speed_ms,
                       w.rainfall_1h_mm,
                       w.visibility_km,
                       COALESCE(recipient_stats.recipient_count, 0) AS recipient_count,
                       COALESCE(receipt_stats.read_count, 0) AS read_count,
                       COALESCE(receipt_stats.acknowledged_count, 0) AS acknowledged_count,
                       user_receipt.read_at IS NOT NULL AS is_read,
                       user_receipt.acknowledged_at IS NOT NULL AS is_acknowledged,
                       user_receipt.acknowledged_at
                FROM operational.alerts a
                JOIN operational.ports p ON p.id = a.port_id
                LEFT JOIN operational.risk_assessments ra ON ra.id = a.risk_assessment_id
                LEFT JOIN operational.weather_readings w ON w.id = ra.weather_reading_id
                LEFT JOIN operational.zones z ON z.id = COALESCE(a.zone_id, ra.zone_id)
                LEFT JOIN operational.simulation_sessions s ON s.id = a.simulation_session_id
                LEFT JOIN operational.alert_receipts user_receipt
                       ON user_receipt.alert_id = a.id AND user_receipt.user_id = @userId
                LEFT JOIN LATERAL (
                    SELECT COUNT(*)::bigint AS recipient_count
                    FROM operational.users u
                    WHERE u.deleted_at IS NULL
                      AND u.status = 'ACTIVE'
                      AND (u.role = 'ADMIN' OR u.assigned_port_id = a.port_id)
                ) recipient_stats ON TRUE
                LEFT JOIN LATERAL (
                    SELECT COUNT(ar.read_at)::bigint AS read_count,
                           COUNT(ar.acknowledged_at)::bigint AS acknowledged_count
                    FROM operational.alert_receipts ar
                    WHERE ar.alert_id = a.id
                ) receipt_stats ON TRUE
                WHERE (
                    @isAdmin
                    OR (
                        @role = 'PORT_MANAGER'
                        AND a.port_id = (
                            SELECT u.assigned_port_id
                            FROM operational.users u
                            WHERE u.id = @userId AND u.deleted_at IS NULL AND u.status = 'ACTIVE'
                        )
                    )
                    OR (
                        @role = 'OPERATOR'
                        AND EXISTS (
                            SELECT 1
                            FROM operational.tasks scoped_task
                            WHERE scoped_task.assigned_user_id = @userId
                              AND scoped_task.port_id = a.port_id
                              AND (
                                  scoped_task.alert_id = a.id
                                  OR (
                                      scoped_task.alert_id IS NULL
                                      AND a.simulation_session_id IS NOT NULL
                                      AND scoped_task.simulation_session_id = a.simulation_session_id
                                      AND (a.zone_id IS NULL OR scoped_task.zone_id = a.zone_id)
                                  )
                              )
                        )
                    )
                )
            )
            SELECT id, port_id, port_code, port_name, zone_id, zone_name,
                   alert_type, severity, title, message, display_created_at, expires_at,
                   beaufort_number, wind_speed_ms, rainfall_1h_mm, visibility_km,
                   recipient_count, read_count, acknowledged_count,
                   is_read, is_acknowledged, acknowledged_at
            FROM alert_feed
            ORDER BY display_created_at DESC
            LIMIT 100;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        command.Parameters.AddWithValue("portId", portId ?? Guid.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AlertReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AlertReadModel(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetFieldValue<DateTimeOffset>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
                reader.IsDBNull(12) ? null : reader.GetInt16(12),
                reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                reader.GetInt64(16),
                reader.GetInt64(17),
                reader.GetInt64(18),
                reader.GetBoolean(19),
                reader.GetBoolean(20),
                reader.IsDBNull(21) ? null : reader.GetFieldValue<DateTimeOffset>(21)));
        }

        return results;
    }

    public async Task<bool> AcknowledgeAlertAsync(
        Guid alertId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH selected_alert AS (
                SELECT a.id
                FROM operational.alerts a
                JOIN operational.users u ON u.id = @userId
                WHERE a.id = @alertId
                  AND u.deleted_at IS NULL
                  AND u.status = 'ACTIVE'
                  AND (u.role = 'ADMIN' OR u.assigned_port_id = a.port_id)
            ),
            upserted_receipt AS (
                INSERT INTO operational.alert_receipts (
                    alert_id, user_id, delivered_at, read_at, acknowledged_at
                )
                SELECT id, @userId, NOW(), NOW(), NOW()
                FROM selected_alert
                ON CONFLICT (alert_id, user_id)
                DO UPDATE SET
                    read_at = COALESCE(operational.alert_receipts.read_at, NOW()),
                    acknowledged_at = COALESCE(operational.alert_receipts.acknowledged_at, NOW())
                RETURNING alert_id
            )
            SELECT EXISTS (SELECT 1 FROM upserted_receipt);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("alertId", alertId);
        command.Parameters.AddWithValue("userId", userId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<IReadOnlyList<AlertNotificationReadModel>> GetHighSeverityNotificationsAsync(
        Guid simulationSessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            SELECT a.id, a.port_id, p.code, p.name,
                   COALESCE(z.name, 'Toàn cảng'), a.severity::text,
                   a.title, a.message, a.created_at,
                   ARRAY_AGG(DISTINCT u.email ORDER BY u.email)
            FROM operational.alerts a
            JOIN operational.ports p ON p.id = a.port_id
            LEFT JOIN operational.zones z ON z.id = a.zone_id
            JOIN operational.users u
              ON u.assigned_port_id = a.port_id
             AND u.role = 'PORT_MANAGER'
             AND u.status = 'ACTIVE'
             AND u.deleted_at IS NULL
            WHERE a.simulation_session_id = @sessionId
              AND a.severity IN ('HIGH', 'CRITICAL')
            GROUP BY a.id, a.port_id, p.code, p.name, z.name,
                     a.severity, a.title, a.message, a.created_at
            ORDER BY a.created_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sessionId", simulationSessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AlertNotificationReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AlertNotificationReadModel(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8), reader.GetFieldValue<string[]>(9)));
        }

        return results;
    }
}

public sealed record AlertReadModel(
    Guid AlertId,
    Guid PortId,
    string PortCode,
    string PortName,
    Guid? ZoneId,
    string? ZoneName,
    string AlertType,
    string Severity,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    short? BeaufortNumber,
    decimal? WindSpeedMs,
    decimal? Rainfall1hMm,
    decimal? VisibilityKm,
    long RecipientCount,
    long ReadCount,
    long AcknowledgedCount,
    bool Read,
    bool Acknowledged,
    DateTimeOffset? AcknowledgedAt);

public sealed record AlertNotificationReadModel(
    Guid AlertId,
    Guid PortId,
    string PortCode,
    string PortName,
    string ZoneName,
    string Severity,
    string Title,
    string Message,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> RecipientEmails);
