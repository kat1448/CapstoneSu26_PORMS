using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class DashboardRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public DashboardRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardSummaryReadModel?> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT p.id AS port_id,
                   p.code AS port_code,
                   p.name AS port_name,
                   COALESCE(ra.final_risk_level::text, p.current_risk_level::text) AS current_risk_level,
                   p.current_operation_mode::text AS current_operation_mode,
                   wr.wind_speed_ms,
                   wr.beaufort_number,
                   wr.rainfall_1h_mm,
                   wr.visibility_km,
                   (
                       SELECT COUNT(*)
                       FROM operational.alerts a
                       WHERE a.port_id = p.id
                         AND (a.expires_at IS NULL OR a.expires_at > NOW())
                   ) AS active_alert_count
            FROM operational.ports p
            LEFT JOIN LATERAL (
                SELECT w.wind_speed_ms,
                       w.beaufort_number,
                       w.rainfall_1h_mm,
                       w.visibility_km,
                       w.observed_at
                FROM operational.weather_readings w
                WHERE w.port_id = p.id
                  AND w.zone_id IS NULL
                  AND w.is_simulation = FALSE
                ORDER BY w.observed_at DESC, w.recorded_at DESC
                LIMIT 1
            ) wr ON TRUE
            LEFT JOIN LATERAL (
                SELECT r.final_risk_level,
                       r.evaluated_at
                FROM operational.risk_assessments r
                WHERE r.port_id = p.id
                  AND r.zone_id IS NULL
                  AND r.is_simulation = FALSE
                ORDER BY r.evaluated_at DESC
                LIMIT 1
            ) ra ON TRUE
            WHERE p.deleted_at IS NULL
            ORDER BY p.code
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DashboardSummaryReadModel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetInt16(6),
            reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            reader.GetInt64(9));
    }
}

public sealed record DashboardSummaryReadModel(
    Guid PortId,
    string PortCode,
    string PortName,
    string CurrentRiskLevel,
    string CurrentOperationMode,
    decimal? WindSpeedMs,
    short? BeaufortNumber,
    decimal? Rainfall1hMm,
    decimal? VisibilityKm,
    long ActiveAlertCount);
