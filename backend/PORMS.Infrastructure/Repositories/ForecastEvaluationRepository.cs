using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class ForecastEvaluationRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public ForecastEvaluationRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ForecastEvaluationRowReadModel>> GetRowsAsync(
        string? portCode,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH forecast_candidates AS (
                SELECT d.name AS dataset_name,
                       d.created_at AS dataset_created_at,
                       p.code AS port_code,
                       p.name AS port_name,
                       s.snapshot_number,
                       (d.starts_at + ((s.snapshot_number - 1) * INTERVAL '1 day')) AS planned_at,
                       s.wind_speed_ms AS forecast_wind_speed_ms,
                       s.rainfall_1h_mm AS forecast_rainfall_mm,
                       s.visibility_km AS forecast_visibility_km,
                       s.beaufort_number AS forecast_beaufort_number,
                       CASE
                           WHEN s.beaufort_number >= 10 OR s.rainfall_1h_mm >= 50 OR COALESCE(s.visibility_km, 99) <= 1.5 THEN 'CRITICAL'
                           WHEN s.beaufort_number >= 8 OR s.rainfall_1h_mm >= 25 OR COALESCE(s.visibility_km, 99) <= 5 THEN 'HIGH'
                           WHEN s.beaufort_number >= 6 OR s.rainfall_1h_mm >= 10 OR COALESCE(s.visibility_km, 99) <= 10 THEN 'MEDIUM'
                           ELSE 'LOW'
                       END AS forecast_risk_level
                FROM operational.simulation_datasets d
                JOIN operational.simulation_snapshots s ON s.dataset_id = d.id
                JOIN operational.ports p ON p.code = UPPER(d.metadata->>'portCode')
                WHERE d.metadata->>'source' = 'forecast-plan'
                  AND (@portCode::text IS NULL OR p.code = @portCode::text)
            ),
            forecast_rows AS (
                SELECT dataset_name,
                       port_code,
                       port_name,
                       snapshot_number,
                       planned_at,
                       forecast_wind_speed_ms,
                       forecast_rainfall_mm,
                       forecast_visibility_km,
                       forecast_beaufort_number,
                       forecast_risk_level
                FROM (
                    SELECT candidate.*,
                           ROW_NUMBER() OVER (
                               PARTITION BY candidate.port_code, candidate.planned_at
                               ORDER BY candidate.dataset_created_at ASC
                           ) AS row_number
                    FROM forecast_candidates candidate
                ) ranked
                WHERE row_number = 1
            )
            SELECT f.dataset_name,
                   f.port_code,
                   f.port_name,
                   f.snapshot_number,
                   f.planned_at,
                   actual.observed_at,
                   f.forecast_wind_speed_ms,
                   actual.wind_speed_ms,
                   ABS(f.forecast_wind_speed_ms - actual.wind_speed_ms),
                   f.forecast_rainfall_mm,
                   actual.rainfall_1h_mm,
                   ABS(f.forecast_rainfall_mm - actual.rainfall_1h_mm),
                   f.forecast_visibility_km,
                   actual.visibility_km,
                   CASE
                       WHEN f.forecast_visibility_km IS NULL OR actual.visibility_km IS NULL THEN NULL
                       ELSE ABS(f.forecast_visibility_km - actual.visibility_km)
                   END,
                   f.forecast_risk_level,
                   actual.actual_risk_level,
                   CASE
                       WHEN actual.actual_risk_level IS NULL THEN NULL
                       ELSE ABS(
                           CASE f.forecast_risk_level
                               WHEN 'LOW' THEN 1
                               WHEN 'MEDIUM' THEN 2
                               WHEN 'HIGH' THEN 3
                               ELSE 4
                           END
                           -
                           CASE actual.actual_risk_level
                               WHEN 'LOW' THEN 1
                               WHEN 'MEDIUM' THEN 2
                               WHEN 'HIGH' THEN 3
                               ELSE 4
                           END
                       )
                   END,
                   actual.data_source,
                   CASE
                       WHEN actual.data_source = 'DEMO_BACKFILL' THEN 'MATCHED_DEMO'
                       WHEN actual.observed_at IS NOT NULL THEN 'MATCHED'
                       WHEN f.planned_at > NOW() THEN 'FUTURE'
                       ELSE 'WAITING_ACTUAL'
                   END
            FROM forecast_rows f
            LEFT JOIN LATERAL (
                SELECT candidate.observed_at,
                       candidate.wind_speed_ms,
                       candidate.rainfall_1h_mm,
                       candidate.visibility_km,
                       candidate.actual_risk_level,
                       candidate.data_source
                FROM (
                    SELECT w.observed_at,
                           w.wind_speed_ms,
                           w.rainfall_1h_mm,
                           w.visibility_km,
                           CASE
                               WHEN w.beaufort_number >= 10 OR w.rainfall_1h_mm >= 50 OR COALESCE(w.visibility_km, 99) <= 1.5 THEN 'CRITICAL'
                               WHEN w.beaufort_number >= 8 OR w.rainfall_1h_mm >= 25 OR COALESCE(w.visibility_km, 99) <= 5 THEN 'HIGH'
                               WHEN w.beaufort_number >= 6 OR w.rainfall_1h_mm >= 10 OR COALESCE(w.visibility_km, 99) <= 10 THEN 'MEDIUM'
                               ELSE 'LOW'
                           END AS actual_risk_level,
                           w.data_source,
                           0 AS source_priority,
                           w.recorded_at
                    FROM operational.weather_readings w
                    JOIN operational.ports p2 ON p2.id = w.port_id
                    WHERE p2.code = f.port_code
                      AND w.zone_id IS NULL
                      AND w.is_simulation = FALSE
                      AND w.observed_at BETWEEN f.planned_at - INTERVAL '12 hours' AND f.planned_at + INTERVAL '12 hours'

                    UNION ALL

                    SELECT f.planned_at,
                           GREATEST(0, f.forecast_wind_speed_ms + ((f.snapshot_number % 3) - 1) * 0.6),
                           GREATEST(0, f.forecast_rainfall_mm + ((f.snapshot_number % 2) * 1.2) - 0.4),
                           CASE
                               WHEN f.forecast_visibility_km IS NULL THEN NULL
                               ELSE GREATEST(0.5, f.forecast_visibility_km + ((f.snapshot_number % 3) - 1) * 0.7)
                           END,
                           f.forecast_risk_level,
                           'DEMO_BACKFILL',
                           1,
                           f.planned_at
                    WHERE f.planned_at <= NOW()
                ) candidate
                ORDER BY candidate.source_priority,
                         ABS(EXTRACT(EPOCH FROM (candidate.observed_at - f.planned_at))) ASC,
                         candidate.recorded_at DESC
                LIMIT 1
            ) actual ON TRUE
            WHERE (@fromDate::timestamptz IS NULL OR f.planned_at >= @fromDate::timestamptz)
              AND (@toDate::timestamptz IS NULL OR f.planned_at <= @toDate::timestamptz)
            ORDER BY f.planned_at DESC, f.port_code, f.snapshot_number
            LIMIT 500;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("portCode", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(portCode) ? DBNull.Value : portCode.Trim().ToUpperInvariant();
        command.Parameters.Add("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = from.HasValue ? from.Value : DBNull.Value;
        command.Parameters.Add("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = to.HasValue ? to.Value : DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<ForecastEvaluationRowReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ForecastEvaluationRowReadModel(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetInt32(17),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.GetString(19)));
        }

        return rows;
    }
}

public sealed record ForecastEvaluationRowReadModel(
    string DatasetName,
    string PortCode,
    string PortName,
    int SnapshotNumber,
    DateTimeOffset PlannedAt,
    DateTimeOffset? ActualObservedAt,
    decimal ForecastWindSpeedMs,
    decimal? ActualWindSpeedMs,
    decimal? WindAbsError,
    decimal ForecastRainfallMm,
    decimal? ActualRainfallMm,
    decimal? RainAbsError,
    decimal? ForecastVisibilityKm,
    decimal? ActualVisibilityKm,
    decimal? VisibilityAbsError,
    string ForecastRiskLevel,
    string? ActualRiskLevel,
    int? RiskScoreError,
    string? ActualDataSource,
    string Status);
