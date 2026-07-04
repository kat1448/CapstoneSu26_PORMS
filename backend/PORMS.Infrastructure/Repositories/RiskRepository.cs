using Npgsql;
using PORMS.Infrastructure.Data;
using System.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class RiskRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public RiskRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RiskConfigReadModel> GetConfigAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var thresholds = new List<RiskThresholdReadModel>();
        const string thresholdSql = """
            SELECT id,
                   factor::text,
                   risk_level::text,
                   comparison_operator::text,
                   threshold_value,
                   unit,
                   description,
                   version,
                   is_enabled,
                   updated_at
            FROM operational.risk_thresholds
            ORDER BY factor, version DESC, risk_level;
            """;

        await using (var command = new NpgsqlCommand(thresholdSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                thresholds.Add(new RiskThresholdReadModel(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetInt32(7),
                    reader.GetBoolean(8),
                    reader.GetFieldValue<DateTimeOffset>(9)));
            }
        }

        var overrides = new List<ZoneThresholdOverrideReadModel>();
        const string overrideSql = """
            SELECT zto.id,
                   zto.zone_id,
                   z.name,
                   z.zone_type::text,
                   zto.factor::text,
                   zto.risk_level::text,
                   zto.comparison_operator::text,
                   zto.threshold_value,
                   zto.unit,
                   zto.is_enabled,
                   zto.updated_at
            FROM operational.zone_threshold_overrides zto
            JOIN operational.zones z ON z.id = zto.zone_id
            WHERE z.deleted_at IS NULL
            ORDER BY z.name, zto.factor, zto.risk_level;
            """;

        await using (var command = new NpgsqlCommand(overrideSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                overrides.Add(new ZoneThresholdOverrideReadModel(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetDecimal(7),
                    reader.GetString(8),
                    reader.GetBoolean(9),
                    reader.GetFieldValue<DateTimeOffset>(10)));
            }
        }

        var zones = new List<RiskConfigZoneReadModel>();
        const string zoneSql = """
            SELECT z.id,
                   z.name,
                   z.zone_type::text,
                   p.name
            FROM operational.zones z
            JOIN operational.ports p ON p.id = z.port_id
            WHERE z.deleted_at IS NULL
              AND p.deleted_at IS NULL
            ORDER BY p.name, z.display_order, z.name;
            """;

        await using (var command = new NpgsqlCommand(zoneSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                zones.Add(new RiskConfigZoneReadModel(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
        }

        return new RiskConfigReadModel(thresholds, overrides, zones);
    }

    public async Task SaveThresholdsAsync(
        IReadOnlyList<SaveRiskThresholdReadModel> thresholds,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        const string sql = """
            INSERT INTO operational.risk_thresholds (
                factor,
                risk_level,
                comparison_operator,
                threshold_value,
                unit,
                description,
                version,
                is_enabled,
                change_reason,
                updated_at
            )
            VALUES (
                @factor::operational.weather_factor_enum,
                @riskLevel::operational.risk_level_enum,
                @comparisonOperator::operational.threshold_operator_enum,
                @thresholdValue,
                @unit,
                @description,
                @version,
                @isEnabled,
                @changeReason,
                NOW()
            )
            ON CONFLICT (factor, risk_level, version) DO UPDATE
            SET comparison_operator = EXCLUDED.comparison_operator,
                threshold_value = EXCLUDED.threshold_value,
                unit = EXCLUDED.unit,
                description = EXCLUDED.description,
                is_enabled = EXCLUDED.is_enabled,
                change_reason = EXCLUDED.change_reason,
                updated_at = NOW();
            """;

        foreach (var threshold in thresholds)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("factor", threshold.Factor.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("riskLevel", threshold.RiskLevel.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("comparisonOperator", NormalizeOperator(threshold.ComparisonOperator));
            command.Parameters.AddWithValue("thresholdValue", threshold.ThresholdValue);
            command.Parameters.AddWithValue("unit", threshold.Unit.Trim());
            command.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(threshold.Description) ? DBNull.Value : threshold.Description.Trim());
            command.Parameters.AddWithValue("version", threshold.Version <= 0 ? 1 : threshold.Version);
            command.Parameters.AddWithValue("isEnabled", threshold.IsEnabled);
            command.Parameters.AddWithValue("changeReason", string.IsNullOrWhiteSpace(changeReason) ? DBNull.Value : changeReason.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveZoneThresholdOverridesAsync(
        Guid zoneId,
        IReadOnlyList<SaveZoneThresholdOverrideReadModel> overrides,
        string? changeReason,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        const string sql = """
            INSERT INTO operational.zone_threshold_overrides (
                zone_id,
                factor,
                risk_level,
                comparison_operator,
                threshold_value,
                unit,
                is_enabled,
                change_reason,
                updated_at
            )
            VALUES (
                @zoneId,
                @factor::operational.weather_factor_enum,
                @riskLevel::operational.risk_level_enum,
                @comparisonOperator::operational.threshold_operator_enum,
                @thresholdValue,
                @unit,
                @isEnabled,
                @changeReason,
                NOW()
            )
            ON CONFLICT (zone_id, factor, risk_level) DO UPDATE
            SET comparison_operator = EXCLUDED.comparison_operator,
                threshold_value = EXCLUDED.threshold_value,
                unit = EXCLUDED.unit,
                is_enabled = EXCLUDED.is_enabled,
                change_reason = EXCLUDED.change_reason,
                updated_at = NOW();
            """;

        foreach (var threshold in overrides)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("zoneId", zoneId);
            command.Parameters.AddWithValue("factor", threshold.Factor.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("riskLevel", threshold.RiskLevel.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("comparisonOperator", NormalizeOperator(threshold.ComparisonOperator));
            command.Parameters.AddWithValue("thresholdValue", threshold.ThresholdValue);
            command.Parameters.AddWithValue("unit", threshold.Unit.Trim());
            command.Parameters.AddWithValue("isEnabled", threshold.IsEnabled);
            command.Parameters.AddWithValue("changeReason", string.IsNullOrWhiteSpace(changeReason) ? DBNull.Value : changeReason.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteZoneThresholdOverrideAsync(
        Guid zoneId,
        Guid overrideId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            DELETE FROM operational.zone_threshold_overrides
            WHERE id = @overrideId
              AND zone_id = @zoneId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("overrideId", overrideId);
        command.Parameters.AddWithValue("zoneId", zoneId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<RiskTrendPointReadModel>> GetTrendAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH hourly AS (
                SELECT DATE_TRUNC('hour', evaluated_at AT TIME ZONE 'Asia/Ho_Chi_Minh') AS local_hour,
                       MAX(CASE final_risk_level::text
                           WHEN 'LOW' THEN 1
                           WHEN 'MEDIUM' THEN 2
                           WHEN 'HIGH' THEN 3
                           WHEN 'CRITICAL' THEN 4
                           ELSE 1
                       END) AS risk_score
                FROM operational.risk_assessments
                WHERE evaluated_at >= NOW() - INTERVAL '24 hours'
                  AND zone_id IS NULL
                  AND is_simulation = FALSE
                GROUP BY local_hour
            )
            SELECT TO_CHAR(local_hour, 'HH24:00') AS hour_label,
                   risk_score::smallint
            FROM hourly
            ORDER BY local_hour;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<RiskTrendPointReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RiskTrendPointReadModel(reader.GetString(0), reader.GetInt16(1)));
        }

        return results;
    }

    private static short RiskScore(string riskLevel)
    {
        return riskLevel switch
        {
            "LOW" => 1,
            "MEDIUM" => 2,
            "HIGH" => 3,
            "CRITICAL" => 4,
            _ => 1
        };
    }

    private static string NormalizeOperator(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            ">" or ">=" or "GTE" => "GTE",
            "<" or "<=" or "LTE" => "LTE",
            _ => throw new ArgumentException($"Unsupported threshold operator '{value}'.", nameof(value))
        };
    }
}

public sealed record RiskTrendPointReadModel(string HourLabel, short RiskScore);
public sealed record RiskConfigReadModel(
    IReadOnlyList<RiskThresholdReadModel> Thresholds,
    IReadOnlyList<ZoneThresholdOverrideReadModel> ZoneOverrides,
    IReadOnlyList<RiskConfigZoneReadModel> Zones);
public sealed record RiskThresholdReadModel(
    Guid Id,
    string Factor,
    string RiskLevel,
    string ComparisonOperator,
    decimal ThresholdValue,
    string Unit,
    string? Description,
    int Version,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);
public sealed record ZoneThresholdOverrideReadModel(
    Guid Id,
    Guid ZoneId,
    string ZoneName,
    string ZoneType,
    string Factor,
    string RiskLevel,
    string ComparisonOperator,
    decimal ThresholdValue,
    string Unit,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);
public sealed record RiskConfigZoneReadModel(
    Guid ZoneId,
    string ZoneName,
    string ZoneType,
    string PortName);
public sealed record SaveRiskThresholdReadModel(
    string Factor,
    string RiskLevel,
    string ComparisonOperator,
    decimal ThresholdValue,
    string Unit,
    string? Description,
    int Version,
    bool IsEnabled);
public sealed record SaveZoneThresholdOverrideReadModel(
    string Factor,
    string RiskLevel,
    string ComparisonOperator,
    decimal ThresholdValue,
    string Unit,
    bool IsEnabled);
