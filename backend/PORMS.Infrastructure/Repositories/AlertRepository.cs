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

    public async Task<IReadOnlyList<AlertReadModel>> GetAlertsAsync(CancellationToken cancellationToken)
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
                               THEN CONCAT('Cảnh báo nguy cơ cao tại ', p.name)
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
                                   WHEN 'HIGH' THEN 'cao'
                                   ELSE LOWER(a.severity::text)
                               END,
                               '. Theo quy trình ứng phó, ',
                               CASE a.severity
                                   WHEN 'CRITICAL' THEN 'đề nghị tạm dừng vận hành tại khu vực và thực hiện ngay nhiệm vụ SOP khẩn cấp.'
                                   WHEN 'HIGH' THEN 'đề nghị hạn chế vận hành tại khu vực và thực hiện các nhiệm vụ SOP được giao.'
                                   ELSE 'tiếp tục theo dõi tình hình.'
                               END)
                           ELSE a.message
                       END AS message,
                       CASE
                           WHEN a.simulation_session_id IS NOT NULL AND a.created_at > NOW() THEN COALESCE(s.created_at, s.started_at, a.created_at)
                           ELSE a.created_at
                       END AS display_created_at,
                       a.expires_at,
                       COUNT(ar.id) AS recipient_count,
                       COUNT(ar.read_at) AS read_count,
                       COUNT(ar.acknowledged_at) AS acknowledged_count
                FROM operational.alerts a
                 JOIN operational.ports p ON p.id = a.port_id
                 LEFT JOIN operational.risk_assessments ra ON ra.id = a.risk_assessment_id
                 LEFT JOIN operational.weather_readings w ON w.id = ra.weather_reading_id
                 LEFT JOIN operational.zones z ON z.id = COALESCE(a.zone_id, ra.zone_id)
                LEFT JOIN operational.simulation_sessions s ON s.id = a.simulation_session_id
                LEFT JOIN operational.alert_receipts ar ON ar.alert_id = a.id
                 GROUP BY a.id, p.code, p.name, ra.zone_id, z.name, s.created_at, s.started_at,
                          w.beaufort_number, w.wind_speed_ms, w.rainfall_1h_mm, w.visibility_km
            )
            SELECT id,
                   port_id,
                   port_code,
                   port_name,
                   zone_id,
                   zone_name,
                   alert_type,
                   severity,
                   title,
                   message,
                   display_created_at,
                   expires_at,
                   recipient_count,
                   read_count,
                   acknowledged_count
            FROM alert_feed
            ORDER BY display_created_at DESC
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
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
                reader.GetInt64(12),
                reader.GetInt64(13),
                reader.GetInt64(14)));
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
                SELECT id
                FROM operational.alerts
                WHERE id = @alertId
            ),
            upserted_receipt AS (
                INSERT INTO operational.alert_receipts (
                    alert_id,
                    user_id,
                    delivered_at,
                    read_at,
                    acknowledged_at
                )
                SELECT
                    id,
                    @userId,
                    NOW(),
                    NOW(),
                    NOW()
                FROM selected_alert
                ON CONFLICT (alert_id, user_id)
                DO UPDATE
                SET read_at = COALESCE(operational.alert_receipts.read_at, NOW()),
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
    long RecipientCount,
    long ReadCount,
    long AcknowledgedCount);
