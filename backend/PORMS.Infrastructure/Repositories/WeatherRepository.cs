using Npgsql;
using NpgsqlTypes;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class WeatherRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public WeatherRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WeatherSnapshotReadModel> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT COALESCE(wind_speed_ms, 0),
                   COALESCE(beaufort_number, 0),
                   wind_direction_deg,
                   wind_gust_ms,
                   COALESCE(rainfall_1h_mm, 0),
                   COALESCE(visibility_km, 0),
                   COALESCE(temperature_c, 0),
                   COALESCE(humidity_pct, 0),
                   pressure_hpa,
                   weather_code,
                   weather_description,
                   observed_at,
                   recorded_at,
                   data_source
            FROM operational.weather_readings
            WHERE zone_id IS NULL
              AND is_simulation = FALSE
            ORDER BY observed_at DESC, recorded_at DESC
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new WeatherSnapshotReadModel(0, 0, null, null, 0, 0, 0, 0, null, null, null, null, null, "OPENWEATHER_API");
        }

        return new WeatherSnapshotReadModel(
            reader.GetDecimal(0),
            reader.GetInt16(1),
            reader.IsDBNull(2) ? null : reader.GetInt16(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetInt16(7),
            reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            reader.IsDBNull(9) ? null : reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetString(13));
    }

    public async Task<IReadOnlyList<OpenWeatherPortReadModel>> GetActiveOpenWeatherPortsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id, latitude, longitude
            FROM operational.ports
            WHERE deleted_at IS NULL
              AND is_active = TRUE
              AND UPPER(weather_source) = 'OPENWEATHER'
            ORDER BY code;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ports = new List<OpenWeatherPortReadModel>();

        while (await reader.ReadAsync(cancellationToken))
        {
            ports.Add(new OpenWeatherPortReadModel(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                reader.GetDecimal(2)));
        }

        return ports;
    }

    public async Task UpsertOpenWeatherReadingAsync(OpenWeatherReadingInput input, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertSql = """
            INSERT INTO operational.weather_readings (
                id,
                port_id,
                zone_id,
                simulation_session_id,
                wind_speed_ms,
                beaufort_number,
                wind_direction_deg,
                wind_gust_ms,
                rainfall_1h_mm,
                temperature_c,
                humidity_pct,
                visibility_km,
                pressure_hpa,
                weather_code,
                weather_description,
                observed_at,
                recorded_at,
                data_source,
                source_record_key,
                raw_payload,
                is_simulation
            )
            VALUES (
                @id,
                @portId,
                NULL,
                NULL,
                @windSpeed,
                @beaufort,
                @windDirection,
                @windGust,
                @rainfall,
                @temperature,
                @humidity,
                @visibility,
                @pressure,
                @weatherCode,
                @weatherDescription,
                @observedAt,
                NOW(),
                'OPENWEATHER_API',
                @sourceRecordKey,
                @rawPayload::jsonb,
                FALSE
            )
            ON CONFLICT DO NOTHING;
            """;

        await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("portId", input.PortId);
            command.Parameters.AddWithValue("windSpeed", input.WindSpeedMs);
            command.Parameters.AddWithValue("beaufort", input.BeaufortNumber);
            command.Parameters.AddWithValue("windDirection", input.WindDirectionDeg.HasValue ? input.WindDirectionDeg.Value : DBNull.Value);
            command.Parameters.AddWithValue("windGust", input.WindGustMs.HasValue ? input.WindGustMs.Value : DBNull.Value);
            command.Parameters.AddWithValue("rainfall", input.Rainfall1hMm);
            command.Parameters.AddWithValue("temperature", input.TemperatureC);
            command.Parameters.AddWithValue("humidity", input.HumidityPct);
            command.Parameters.AddWithValue("visibility", input.VisibilityKm);
            command.Parameters.AddWithValue("pressure", input.PressureHpa.HasValue ? input.PressureHpa.Value : DBNull.Value);
            command.Parameters.AddWithValue("weatherCode", input.WeatherCode.HasValue ? input.WeatherCode.Value : DBNull.Value);
            command.Parameters.AddWithValue("weatherDescription", string.IsNullOrWhiteSpace(input.WeatherDescription) ? DBNull.Value : input.WeatherDescription);
            command.Parameters.AddWithValue("observedAt", input.ObservedAt);
            command.Parameters.AddWithValue("sourceRecordKey", input.SourceRecordKey);
            command.Parameters.Add("rawPayload", NpgsqlDbType.Jsonb).Value = input.RawPayload;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string updatePortSql = """
            UPDATE operational.ports
            SET last_weather_fetch_at = NOW(),
                last_weather_fetch_ok = TRUE,
                updated_at = NOW()
            WHERE id = @portId;
            """;

        await using (var command = new NpgsqlCommand(updatePortSql, connection, transaction))
        {
            command.Parameters.AddWithValue("portId", input.PortId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed record WeatherSnapshotReadModel(
    decimal WindSpeedMs,
    short BeaufortNumber,
    short? WindDirectionDeg,
    decimal? WindGustMs,
    decimal Rainfall1hMm,
    decimal VisibilityKm,
    decimal TemperatureC,
    short HumidityPct,
    decimal? PressureHpa,
    int? WeatherCode,
    string? WeatherDescription,
    DateTimeOffset? ObservedAt,
    DateTimeOffset? RecordedAt,
    string DataSource);

public sealed record OpenWeatherPortReadModel(
    Guid PortId,
    decimal Latitude,
    decimal Longitude);

public sealed record OpenWeatherReadingInput(
    Guid PortId,
    decimal WindSpeedMs,
    short BeaufortNumber,
    short? WindDirectionDeg,
    decimal? WindGustMs,
    decimal Rainfall1hMm,
    decimal TemperatureC,
    short HumidityPct,
    decimal VisibilityKm,
    decimal? PressureHpa,
    int? WeatherCode,
    string? WeatherDescription,
    DateTimeOffset ObservedAt,
    string SourceRecordKey,
    string RawPayload);
