using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class OperationEventRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public OperationEventRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<OperationEventReadModel>> GetOperationEventsAsync(bool simulationOnly, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT e.id,
                   e.event_type,
                   e.port_id,
                   p.code AS port_code,
                   p.name AS port_name,
                   e.zone_id,
                   z.name AS zone_name,
                   e.actor_user_id,
                   u.full_name AS actor_name,
                   e.entity_type,
                   e.entity_id,
                   e.summary,
                   e.occurred_at,
                   e.simulation_session_id
            FROM operational.operation_events e
            LEFT JOIN operational.ports p ON p.id = e.port_id
            LEFT JOIN operational.zones z ON z.id = e.zone_id
            LEFT JOIN operational.users u ON u.id = e.actor_user_id
            WHERE (@simulationOnly = TRUE AND e.simulation_session_id IS NOT NULL)
               OR (@simulationOnly = FALSE AND e.simulation_session_id IS NULL)
            ORDER BY e.occurred_at DESC
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("simulationOnly", simulationOnly);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<OperationEventReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OperationEventReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.GetString(11),
                reader.GetFieldValue<DateTimeOffset>(12),
                reader.IsDBNull(13) ? null : reader.GetGuid(13)));
        }

        return results;
    }
}

public sealed record OperationEventReadModel(
    Guid OperationEventId,
    string EventType,
    Guid? PortId,
    string? PortCode,
    string? PortName,
    Guid? ZoneId,
    string? ZoneName,
    Guid? ActorUserId,
    string? ActorName,
    string? EntityType,
    Guid? EntityId,
    string Summary,
    DateTimeOffset OccurredAt,
    Guid? SimulationSessionId);
