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
                   COALESCE(rainfall_1h_mm, 0),
                   COALESCE(visibility_km, 0),
                   COALESCE(temperature_c, 0),
                   COALESCE(humidity_pct, 0)
            FROM operational.weather_readings
            ORDER BY observed_at DESC, recorded_at DESC
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new WeatherSnapshotReadModel(0, 0, 0, 0, 0);
        }

        return new WeatherSnapshotReadModel(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetInt16(4));
    }
}

public sealed record WeatherSnapshotReadModel(
    decimal WindSpeedMs,
    decimal Rainfall1hMm,
    decimal VisibilityKm,
    decimal TemperatureC,
    short HumidityPct);
