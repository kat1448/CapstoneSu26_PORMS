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
            SELECT port_id,
                   port_code,
                   port_name,
                   current_risk_level,
                   current_operation_mode,
                   wind_speed_ms,
                   beaufort_number,
                   rainfall_1h_mm,
                   visibility_km,
                   active_alert_count
            FROM operational.v_port_current_state
            ORDER BY port_code
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
