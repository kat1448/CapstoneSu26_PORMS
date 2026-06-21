using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class PortRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public PortRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<PortSummaryReadModel>> GetPortsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT port_id,
                   port_code,
                   port_name,
                   current_risk_level,
                   current_operation_mode,
                   is_active,
                   active_alert_count,
                   last_weather_fetch_at
            FROM operational.v_port_current_state
            ORDER BY port_code;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<PortSummaryReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PortSummaryReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return results;
    }

    public async Task<IReadOnlyList<ZoneReadModel>> GetZonesAsync(Guid portId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id,
                   port_id,
                   name,
                   zone_type,
                   current_risk_level,
                   is_restricted,
                   restriction_reason,
                   is_active,
                   capacity_value,
                   capacity_unit,
                   display_order,
                   EXISTS (
                       SELECT 1
                       FROM operational.zone_threshold_overrides zto
                       WHERE zto.zone_id = z.id
                         AND zto.is_enabled = TRUE
                   ) AS override_enabled
            FROM operational.zones z
            WHERE port_id = @portId
              AND deleted_at IS NULL
            ORDER BY display_order, name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("portId", portId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<ZoneReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ZoneReadModel(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetInt16(10),
                reader.GetBoolean(11)));
        }

        return results;
    }
}

public sealed record PortSummaryReadModel(
    Guid PortId,
    string PortCode,
    string PortName,
    string CurrentRiskLevel,
    string CurrentOperationMode,
    bool IsActive,
    long ActiveAlertCount,
    DateTimeOffset? LastWeatherFetchAt);

public sealed record ZoneReadModel(
    Guid ZoneId,
    Guid PortId,
    string ZoneName,
    string ZoneType,
    string CurrentRiskLevel,
    bool IsRestricted,
    string? RestrictionReason,
    bool IsActive,
    decimal? CapacityValue,
    string? CapacityUnit,
    short DisplayOrder,
    bool OverrideEnabled);
