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
            SELECT state.port_id,
                   state.port_code,
                   state.port_name,
                   port.latitude,
                   port.longitude,
                   state.current_risk_level,
                   state.current_operation_mode,
                   state.is_active,
                   state.active_alert_count,
                   state.last_weather_fetch_at
            FROM operational.v_port_current_state state
            JOIN operational.ports port ON port.id = state.port_id
            ORDER BY state.port_code;
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
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetBoolean(7),
                reader.GetInt64(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9)));
        }

        return results;
    }

    public async Task<PortSummaryReadModel> CreatePortAsync(CreatePortReadModel input, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertPortSql = """
            INSERT INTO operational.ports (
                code,
                name,
                address,
                latitude,
                longitude,
                timezone,
                weather_source,
                weather_station_id,
                is_active
            )
            VALUES (
                @code,
                @name,
                @address,
                @latitude,
                @longitude,
                @timezone,
                @weatherSource,
                @weatherStationId,
                @isActive
            )
            RETURNING id,
                      code,
                      name,
                      latitude,
                      longitude,
                      current_risk_level,
                      current_operation_mode,
                      is_active,
                      last_weather_fetch_at;
            """;

        await using var portCommand = new NpgsqlCommand(insertPortSql, connection, transaction);
        portCommand.Parameters.AddWithValue("code", input.Code.Trim().ToUpperInvariant());
        portCommand.Parameters.AddWithValue("name", input.Name.Trim());
        portCommand.Parameters.AddWithValue("address", string.IsNullOrWhiteSpace(input.Address) ? DBNull.Value : input.Address.Trim());
        portCommand.Parameters.AddWithValue("latitude", input.Latitude);
        portCommand.Parameters.AddWithValue("longitude", input.Longitude);
        portCommand.Parameters.AddWithValue("timezone", input.Timezone.Trim());
        portCommand.Parameters.AddWithValue("weatherSource", input.WeatherSource.Trim());
        portCommand.Parameters.AddWithValue("weatherStationId", string.IsNullOrWhiteSpace(input.WeatherStationId) ? DBNull.Value : input.WeatherStationId.Trim());
        portCommand.Parameters.AddWithValue("isActive", input.IsActive);

        await using var reader = await portCommand.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var created = new PortSummaryReadModel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            0,
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));
        await reader.DisposeAsync();

        const string insertZoneSql = """
            INSERT INTO operational.zones (
                port_id,
                name,
                zone_type,
                capacity_value,
                capacity_unit,
                latitude,
                longitude,
                display_order
            )
            VALUES (
                @portId,
                @name,
                @zoneType::operational.zone_type_enum,
                @capacityValue,
                @capacityUnit,
                @latitude,
                @longitude,
                @displayOrder
            );
            """;

        foreach (var zone in input.Zones)
        {
            await using var zoneCommand = new NpgsqlCommand(insertZoneSql, connection, transaction);
            zoneCommand.Parameters.AddWithValue("portId", created.PortId);
            zoneCommand.Parameters.AddWithValue("name", zone.Name.Trim());
            zoneCommand.Parameters.AddWithValue("zoneType", zone.ZoneType);
            zoneCommand.Parameters.AddWithValue("capacityValue", zone.CapacityValue.HasValue ? zone.CapacityValue.Value : DBNull.Value);
            zoneCommand.Parameters.AddWithValue("capacityUnit", string.IsNullOrWhiteSpace(zone.CapacityUnit) ? DBNull.Value : zone.CapacityUnit.Trim());
            zoneCommand.Parameters.AddWithValue("latitude", zone.Latitude.HasValue ? zone.Latitude.Value : DBNull.Value);
            zoneCommand.Parameters.AddWithValue("longitude", zone.Longitude.HasValue ? zone.Longitude.Value : DBNull.Value);
            zoneCommand.Parameters.AddWithValue("displayOrder", zone.DisplayOrder);
            await zoneCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return created;
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
                   latitude,
                   longitude,
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
                reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                reader.GetBoolean(13)));
        }

        return results;
    }

    public async Task<ZoneReadModel?> UpdateZoneAsync(
        Guid portId,
        Guid zoneId,
        UpdateZoneReadModel input,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.zones
            SET name = @name,
                zone_type = @zoneType::operational.zone_type_enum,
                capacity_value = @capacityValue,
                capacity_unit = @capacityUnit,
                latitude = @latitude,
                longitude = @longitude,
                display_order = @displayOrder,
                is_active = @isActive,
                updated_at = NOW()
            WHERE id = @zoneId
              AND port_id = @portId
              AND deleted_at IS NULL
            RETURNING id,
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
                      latitude,
                      longitude,
                      EXISTS (
                          SELECT 1
                          FROM operational.zone_threshold_overrides zto
                          WHERE zto.zone_id = operational.zones.id
                            AND zto.is_enabled = TRUE
                      ) AS override_enabled;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("zoneId", zoneId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("name", input.Name.Trim());
        command.Parameters.AddWithValue("zoneType", input.ZoneType);
        command.Parameters.AddWithValue("capacityValue", input.CapacityValue.HasValue ? input.CapacityValue.Value : DBNull.Value);
        command.Parameters.AddWithValue("capacityUnit", string.IsNullOrWhiteSpace(input.CapacityUnit) ? DBNull.Value : input.CapacityUnit.Trim());
        command.Parameters.AddWithValue("latitude", input.Latitude.HasValue ? input.Latitude.Value : DBNull.Value);
        command.Parameters.AddWithValue("longitude", input.Longitude.HasValue ? input.Longitude.Value : DBNull.Value);
        command.Parameters.AddWithValue("displayOrder", input.DisplayOrder);
        command.Parameters.AddWithValue("isActive", input.IsActive);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadZone(reader);
    }

    public async Task<bool> DeleteZoneAsync(Guid portId, Guid zoneId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.zones
            SET deleted_at = NOW(),
                updated_at = NOW()
            WHERE id = @zoneId
              AND port_id = @portId
              AND deleted_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("zoneId", zoneId);
        command.Parameters.AddWithValue("portId", portId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static ZoneReadModel ReadZone(NpgsqlDataReader reader) =>
        new(
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
            reader.IsDBNull(11) ? null : reader.GetDecimal(11),
            reader.IsDBNull(12) ? null : reader.GetDecimal(12),
            reader.GetBoolean(13));
}

public sealed record PortSummaryReadModel(
    Guid PortId,
    string PortCode,
    string PortName,
    decimal Latitude,
    decimal Longitude,
    string CurrentRiskLevel,
    string CurrentOperationMode,
    bool IsActive,
    long ActiveAlertCount,
    DateTimeOffset? LastWeatherFetchAt);

public sealed record CreatePortReadModel(
    string Code,
    string Name,
    string? Address,
    decimal Latitude,
    decimal Longitude,
    string Timezone,
    string WeatherSource,
    string? WeatherStationId,
    bool IsActive,
    IReadOnlyList<CreateZoneReadModel> Zones);

public sealed record CreateZoneReadModel(
    string Name,
    string ZoneType,
    decimal? CapacityValue,
    string? CapacityUnit,
    decimal? Latitude,
    decimal? Longitude,
    short DisplayOrder);

public sealed record UpdateZoneReadModel(
    string Name,
    string ZoneType,
    decimal? CapacityValue,
    string? CapacityUnit,
    decimal? Latitude,
    decimal? Longitude,
    short DisplayOrder,
    bool IsActive);

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
    decimal? Latitude,
    decimal? Longitude,
    bool OverrideEnabled);
