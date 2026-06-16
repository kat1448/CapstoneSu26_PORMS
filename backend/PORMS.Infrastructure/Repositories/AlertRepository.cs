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
                   created_at,
                   expires_at,
                   recipient_count,
                   read_count,
                   acknowledged_count
            FROM operational.v_alert_feed
            ORDER BY created_at DESC
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
