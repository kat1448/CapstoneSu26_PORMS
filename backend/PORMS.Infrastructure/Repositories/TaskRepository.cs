using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class TaskRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public TaskRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TaskLogReadModel>> GetTasksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT t.id,
                   t.task_code,
                   t.port_id,
                   p.code AS port_code,
                   p.name AS port_name,
                   t.zone_id,
                   z.name AS zone_name,
                   t.title,
                   t.description,
                   t.priority::text AS priority,
                   t.status::text AS status,
                   t.assigned_user_id,
                   u.full_name AS assigned_user_name,
                   t.assigned_team,
                   t.due_at,
                   t.simulation_session_id,
                   t.created_at,
                   t.updated_at
            FROM operational.tasks t
            JOIN operational.ports p ON p.id = t.port_id
            LEFT JOIN operational.zones z ON z.id = t.zone_id
            LEFT JOIN operational.users u ON u.id = t.assigned_user_id
            ORDER BY t.created_at DESC, t.task_code DESC
            LIMIT 100;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<TaskLogReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TaskLogReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetGuid(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
                reader.IsDBNull(15) ? null : reader.GetGuid(15),
                reader.GetFieldValue<DateTimeOffset>(16),
                reader.GetFieldValue<DateTimeOffset>(17)));
        }

        return results;
    }
}

public sealed record TaskLogReadModel(
    Guid TaskId,
    string TaskCode,
    Guid PortId,
    string PortCode,
    string PortName,
    Guid? ZoneId,
    string? ZoneName,
    string Title,
    string? Description,
    string Priority,
    string Status,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? AssignedTeam,
    DateTimeOffset? DueAt,
    Guid? SimulationSessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
