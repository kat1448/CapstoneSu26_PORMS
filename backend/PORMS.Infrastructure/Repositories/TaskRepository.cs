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

    public async Task<IReadOnlyList<TaskLogReadModel>> GetTasksAsync(
        Guid userId,
        string role,
        Guid? portId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var sql = $"""
            {TaskSelectSql}
            WHERE @isAdmin
               OR (
                    t.port_id = (
                        SELECT scope_user.assigned_port_id
                        FROM operational.users scope_user
                        WHERE scope_user.id = @userId
                          AND scope_user.deleted_at IS NULL
                          AND scope_user.status = 'ACTIVE'
                    )
                    AND (@role <> 'OPERATOR' OR t.assigned_user_id = @userId)
               )
            ORDER BY t.created_at DESC, t.task_code DESC
            LIMIT 100;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        command.Parameters.AddWithValue("portId", portId ?? Guid.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await ReadTaskListAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskLogReadModel>> GetTasksByAlertAsync(
        Guid alertId,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var sql = $"""
            {TaskSelectSql}
            JOIN operational.alerts a ON a.id = @alertId
            WHERE (
                   t.alert_id = a.id
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
              )
              AND (
                   @isAdmin
                   OR (
                       @role = 'PORT_MANAGER'
                       AND t.port_id = (
                           SELECT scope_user.assigned_port_id
                           FROM operational.users scope_user
                           WHERE scope_user.id = @userId
                             AND scope_user.deleted_at IS NULL
                             AND scope_user.status = 'ACTIVE'
                       )
                   )
                   OR (@role = 'OPERATOR' AND t.assigned_user_id = @userId)
              )
            ORDER BY t.created_at DESC, t.task_code DESC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("alertId", alertId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadTaskListAsync(reader, cancellationToken);
    }

    public async Task<TaskLogReadModel?> GetTaskAsync(
        Guid taskId,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        var sql = $"""
            {TaskSelectSql}
            WHERE t.id = @taskId
              AND (
                  @isAdmin
                  OR (
                      @role = 'PORT_MANAGER'
                      AND t.port_id = (
                          SELECT scope_user.assigned_port_id
                          FROM operational.users scope_user
                          WHERE scope_user.id = @userId
                            AND scope_user.deleted_at IS NULL
                            AND scope_user.status = 'ACTIVE'
                      )
                  )
                  OR (@role = 'OPERATOR' AND t.assigned_user_id = @userId)
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTask(reader) : null;
    }

    public async Task<IReadOnlyList<TaskAssigneeReadModel>> GetAssignableUsersAsync(
        Guid actorUserId,
        string role,
        Guid? portId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT u.id, u.full_name, u.email, u.role::text, u.assigned_port_id, p.name
            FROM operational.users u
            LEFT JOIN operational.ports p ON p.id = u.assigned_port_id
            WHERE u.deleted_at IS NULL
              AND u.status = 'ACTIVE'
              AND u.role = 'OPERATOR'
              AND (
                  @isAdmin
                  OR u.assigned_port_id = (
                      SELECT manager.assigned_port_id
                      FROM operational.users manager
                      WHERE manager.id = @actorUserId
                        AND manager.deleted_at IS NULL
                        AND manager.status = 'ACTIVE'
                  )
              )
            ORDER BY p.name, u.full_name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("isAdmin", role == "ADMIN");
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("portId", portId ?? Guid.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<TaskAssigneeReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TaskAssigneeReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return results;
    }

    public async Task<TaskLogReadModel?> AssignTaskAsync(
        Guid taskId,
        Guid? assignedUserId,
        string? assignedTeam,
        DateTimeOffset? dueAt,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks t
            SET assigned_user_id = @assignedUserId,
                assigned_team = NULLIF(BTRIM(@assignedTeam), ''),
                due_at = @dueAt,
                updated_at = NOW()
            WHERE t.id = @taskId
              AND t.status = 'NEW'
              AND EXISTS (
                  SELECT 1
                  FROM operational.users actor
                  WHERE actor.id = @actorUserId
                    AND actor.deleted_at IS NULL
                    AND actor.status = 'ACTIVE'
                    AND (actor.role = 'ADMIN' OR (actor.role = 'PORT_MANAGER' AND actor.assigned_port_id = t.port_id))
              )
              AND (
                  @assignedUserId IS NULL
                  OR EXISTS (
                      SELECT 1
                      FROM operational.users assignee
                      WHERE assignee.id = @assignedUserId
                        AND assignee.deleted_at IS NULL
                        AND assignee.status = 'ACTIVE'
                        AND assignee.role = 'OPERATOR'
                        AND assignee.assigned_port_id = t.port_id
                  )
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "assignedUserId", assignedUserId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("assignedTeam", assignedTeam ?? string.Empty);
        AddNullableDateTimeOffset(command, "dueAt", dueAt);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
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
              AND status = 'NEW'
              AND EXISTS (
                  SELECT 1 FROM operational.users actor
                  WHERE actor.id = @actorUserId
                    AND actor.status = 'ACTIVE'
                    AND actor.deleted_at IS NULL
                    AND actor.role = 'OPERATOR'
                    AND operational.tasks.assigned_user_id = actor.id
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "actorUserId", actorUserId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        return await GetTaskByIdAsync(taskId, cancellationToken);
    }

    public async Task<TaskLogReadModel?> StartTaskAsync(
        Guid taskId,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.tasks
            SET status = 'IN_PROGRESS',
                started_at = COALESCE(started_at, NOW()),
                updated_at = NOW()
            WHERE id = @taskId
              AND status = 'ACKNOWLEDGED'
              AND EXISTS (
                  SELECT 1 FROM operational.users actor
                  WHERE actor.id = @actorUserId
                    AND actor.status = 'ACTIVE'
                    AND actor.deleted_at IS NULL
                    AND actor.role = 'OPERATOR'
                    AND operational.tasks.assigned_user_id = actor.id
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "actorUserId", actorUserId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
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
              AND status = 'IN_PROGRESS'
              AND EXISTS (
                  SELECT 1 FROM operational.users actor
                  WHERE actor.id = @actorUserId
                    AND actor.status = 'ACTIVE'
                    AND actor.deleted_at IS NULL
                    AND actor.role = 'OPERATOR'
                    AND operational.tasks.assigned_user_id = actor.id
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        AddNullableGuid(command, "actorUserId", actorUserId);
        command.Parameters.AddWithValue("completionNote", completionNote ?? string.Empty);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
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
    string Role,
    Guid? PortId,
    string? PortName);
