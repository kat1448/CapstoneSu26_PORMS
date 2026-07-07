using Npgsql;
using NpgsqlTypes;
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

        var sql = $"""
            {TaskSelectSql}
            ORDER BY t.created_at DESC, t.task_code DESC
            LIMIT 100;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await ReadTaskListAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskLogReadModel>> GetTasksByAlertAsync(
        Guid alertId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var sql = $"""
            {TaskSelectSql}
            JOIN operational.alerts a ON a.id = @alertId
            WHERE t.alert_id = a.id
               OR (
                   t.alert_id IS NULL
                   AND a.sop_execution_id IS NOT NULL
                   AND t.sop_execution_id = a.sop_execution_id
               )
               OR (
                   t.alert_id IS NULL
                   AND a.simulation_session_id IS NOT NULL
                   AND t.simulation_session_id = a.simulation_session_id
                   AND t.port_id = a.port_id
                   AND (a.zone_id IS NULL OR t.zone_id = a.zone_id)
               )
            ORDER BY t.created_at DESC, t.task_code DESC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("alertId", alertId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadTaskListAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskAssigneeReadModel>> GetAssignableUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id, full_name, email, role
            FROM operational.users
            WHERE deleted_at IS NULL
              AND status = 'ACTIVE'
            ORDER BY full_name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<TaskAssigneeReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TaskAssigneeReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return results;
    }

    public async Task<TaskLogReadModel?> AssignTaskAsync(
        Guid taskId,
        Guid? assignedUserId,
        string? assignedTeam,
        DateTimeOffset? dueAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks
            SET assigned_user_id = (
                    SELECT id FROM operational.users
                    WHERE id = @assignedUserId AND deleted_at IS NULL
                ),
                assigned_team = NULLIF(BTRIM(@assignedTeam), ''),
                due_at = @dueAt,
                updated_at = NOW()
            WHERE id = @taskId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "assignedUserId", assignedUserId);
        command.Parameters.AddWithValue("assignedTeam", assignedTeam ?? string.Empty);
        AddNullableDateTimeOffset(command, "dueAt", dueAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(taskId, cancellationToken);
    }

    public async Task<TaskLogReadModel?> AcknowledgeTaskAsync(
        Guid taskId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks
            SET status = 'ACKNOWLEDGED',
                acknowledged_by_user_id = (
                    SELECT id FROM operational.users
                    WHERE id = @actorUserId AND deleted_at IS NULL
                ),
                acknowledged_at = COALESCE(acknowledged_at, NOW()),
                updated_at = NOW()
            WHERE id = @taskId
              AND status IN ('NEW', 'ACKNOWLEDGED');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "actorUserId", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(taskId, cancellationToken);
    }

    public async Task<TaskLogReadModel?> StartTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks
            SET status = 'IN_PROGRESS',
                started_at = COALESCE(started_at, NOW()),
                updated_at = NOW()
            WHERE id = @taskId
              AND status IN ('NEW', 'ACKNOWLEDGED', 'IN_PROGRESS');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(taskId, cancellationToken);
    }

    public async Task<TaskLogReadModel?> CompleteTaskAsync(
        Guid taskId,
        Guid? actorUserId,
        string? completionNote,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks
            SET status = 'COMPLETED',
                completed_by_user_id = (
                    SELECT id FROM operational.users
                    WHERE id = @actorUserId AND deleted_at IS NULL
                ),
                completed_at = COALESCE(completed_at, NOW()),
                completion_note = NULLIF(BTRIM(@completionNote), ''),
                updated_at = NOW()
            WHERE id = @taskId
              AND status IN ('NEW', 'ACKNOWLEDGED', 'IN_PROGRESS', 'COMPLETED');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "actorUserId", actorUserId);
        command.Parameters.AddWithValue("completionNote", completionNote ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetTaskByIdAsync(taskId, cancellationToken);
    }

    private async Task<TaskLogReadModel?> GetTaskByIdAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var sql = $"""
            {TaskSelectSql}
            WHERE t.id = @taskId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTask(reader) : null;
    }

    private const string TaskSelectSql = """
        SELECT t.id,
               t.task_code,
               t.alert_id,
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
               u.email AS assigned_user_email,
               t.assigned_team,
               t.acknowledged_by_user_id,
               t.acknowledged_at,
               t.started_at,
               t.completed_by_user_id,
               t.completed_at,
               t.completion_note,
               t.due_at,
               t.simulation_session_id,
               t.created_at,
               t.updated_at
        FROM operational.tasks t
        JOIN operational.ports p ON p.id = t.port_id
        LEFT JOIN operational.zones z ON z.id = t.zone_id
        LEFT JOIN operational.users u ON u.id = t.assigned_user_id
        """;

    private static async Task<IReadOnlyList<TaskLogReadModel>> ReadTaskListAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var results = new List<TaskLogReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadTask(reader));
        }

        return results;
    }

    private static TaskLogReadModel ReadTask(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetGuid(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetGuid(16),
            reader.IsDBNull(17) ? null : reader.GetFieldValue<DateTimeOffset>(17),
            reader.IsDBNull(18) ? null : reader.GetFieldValue<DateTimeOffset>(18),
            reader.IsDBNull(19) ? null : reader.GetGuid(19),
            reader.IsDBNull(20) ? null : reader.GetFieldValue<DateTimeOffset>(20),
            reader.IsDBNull(21) ? null : reader.GetString(21),
            reader.IsDBNull(22) ? null : reader.GetFieldValue<DateTimeOffset>(22),
            reader.IsDBNull(23) ? null : reader.GetGuid(23),
            reader.GetFieldValue<DateTimeOffset>(24),
            reader.GetFieldValue<DateTimeOffset>(25));

    private static void AddNullableGuid(NpgsqlCommand command, string parameterName, Guid? value)
    {
        command.Parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Uuid)
        {
            Value = value.HasValue ? value.Value : DBNull.Value
        });
    }

    private static void AddNullableDateTimeOffset(NpgsqlCommand command, string parameterName, DateTimeOffset? value)
    {
        command.Parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.TimestampTz)
        {
            Value = value.HasValue ? value.Value : DBNull.Value
        });
    }
}

public sealed record TaskLogReadModel(
    Guid TaskId,
    string TaskCode,
    Guid? AlertId,
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
    string? AssignedUserEmail,
    string? AssignedTeam,
    Guid? AcknowledgedByUserId,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? StartedAt,
    Guid? CompletedByUserId,
    DateTimeOffset? CompletedAt,
    string? CompletionNote,
    DateTimeOffset? DueAt,
    Guid? SimulationSessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskAssigneeReadModel(
    Guid UserId,
    string FullName,
    string Email,
    string Role);
