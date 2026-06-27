using Npgsql;
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
