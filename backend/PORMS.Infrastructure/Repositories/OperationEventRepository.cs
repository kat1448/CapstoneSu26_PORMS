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

    public async Task<IReadOnlyList<OperationEventReadModel>> GetOperationEventsAsync(
        bool simulationOnly,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
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
                   e.simulation_session_id,
                   sd.name AS simulation_dataset_name
            FROM operational.operation_events e
            LEFT JOIN operational.ports p ON p.id = e.port_id
            LEFT JOIN operational.zones z ON z.id = e.zone_id
            LEFT JOIN operational.users u ON u.id = e.actor_user_id
            LEFT JOIN operational.simulation_sessions ss ON ss.id = e.simulation_session_id
            LEFT JOIN operational.simulation_datasets sd ON sd.id = ss.dataset_id
            WHERE (
                    (@simulationOnly = TRUE AND e.simulation_session_id IS NOT NULL)
                    OR (@simulationOnly = FALSE AND e.simulation_session_id IS NULL)
                  )
              AND NOT (
                    @simulationOnly = FALSE
                    AND e.event_type = 'WEATHER_FETCHED'
                    AND e.port_id IS NULL
                  )
              AND (
                    @role = 'ADMIN'
                    OR (
                        @role = 'PORT_MANAGER'
                        AND e.port_id = (
                            SELECT assigned_port_id
                            FROM operational.users
                            WHERE id = @userId
                              AND deleted_at IS NULL
                              AND status = 'ACTIVE'
                        )
                    )
                    OR (
                        @role = 'OPERATOR'
                        AND e.entity_type = 'TASK'
                        AND EXISTS (
                            SELECT 1
                            FROM operational.tasks scoped_task
                            WHERE scoped_task.id = e.entity_id
                              AND scoped_task.assigned_user_id = @userId
                        )
                    )
                  )
            ORDER BY e.occurred_at DESC
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("simulationOnly", simulationOnly);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
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
                reader.IsDBNull(13) ? null : reader.GetGuid(13),
                reader.IsDBNull(14) ? null : reader.GetString(14)));
        }

        return results;
    }

    public async Task RecordTaskEventAsync(
        TaskLogReadModel task,
        Guid actorUserId,
        string eventType,
        string summary,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO operational.operation_events (
                id,
                event_type,
                port_id,
                zone_id,
                actor_user_id,
                entity_type,
                entity_id,
                summary,
                payload,
                simulation_session_id,
                occurred_at
            ) VALUES (
                @id,
                @eventType,
                @portId,
                @zoneId,
                @actorUserId,
                'TASK',
                @taskId,
                @summary,
                jsonb_build_object(
                    'taskCode', @taskCode,
                    'status', @status,
                    'assignedUserId', @assignedUserId
                ),
                @simulationSessionId,
                NOW()
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("portId", task.PortId);
        AddNullableGuid(command, "zoneId", task.ZoneId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("taskId", task.TaskId);
        command.Parameters.AddWithValue("summary", summary);
        command.Parameters.AddWithValue("taskCode", task.TaskCode);
        command.Parameters.AddWithValue("status", task.Status);
        AddNullableGuid(command, "assignedUserId", task.AssignedUserId);
        AddNullableGuid(command, "simulationSessionId", task.SimulationSessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordReportExportAsync(
        Guid actorUserId, Guid? portId, string reportType, string format, string filterSummary,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO operational.operation_events
                (id, event_type, port_id, actor_user_id, entity_type, summary, payload, occurred_at)
            VALUES
                (@id, 'REPORT_EXPORTED', @portId, @actorUserId, 'REPORT', @summary,
                 jsonb_build_object('reportType', @reportType, 'format', @format, 'filters', @filters), NOW());
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        AddNullableGuid(command, "portId", portId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("summary", $"Đã xuất báo cáo {reportType} định dạng {format}. Bộ lọc: {filterSummary}");
        command.Parameters.AddWithValue("reportType", reportType);
        command.Parameters.AddWithValue("format", format);
        command.Parameters.AddWithValue("filters", filterSummary);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullableGuid(NpgsqlCommand command, string name, Guid? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlTypes.NpgsqlDbType.Uuid);
        parameter.Value = value.HasValue ? value.Value : DBNull.Value;
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
    Guid? SimulationSessionId,
    string? SimulationDatasetName);
