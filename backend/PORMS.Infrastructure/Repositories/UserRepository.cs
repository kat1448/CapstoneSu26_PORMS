using Npgsql;
using NpgsqlTypes;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class UserRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public UserRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<UserSummaryReadModel>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT u.id,
                   u.email,
                   u.full_name,
                   u.role,
                   u.status,
                   u.assigned_port_id,
                   COALESCE(p.name, 'Tất cả') AS port_name,
                   u.last_login_at
            FROM operational.users u
            LEFT JOIN operational.ports p ON p.id = u.assigned_port_id
            WHERE u.deleted_at IS NULL
            ORDER BY u.full_name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<UserSummaryReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadUserSummary(reader));
        }

        return results;
    }

    public async Task<UserSummaryReadModel> CreateUserAsync(
        CreateUserReadModel input,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH inserted AS (
                INSERT INTO operational.users (
                    email,
                    full_name,
                    password_hash,
                    role,
                    status,
                    assigned_port_id,
                    password_changed_at
                )
                VALUES (
                    @email,
                    @fullName,
                    @passwordHash,
                    @role::operational.user_role_enum,
                    @status::operational.user_status_enum,
                    @portId,
                    NOW()
                )
                RETURNING id, email, full_name, role, status, assigned_port_id, last_login_at
            )
            SELECT i.id,
                   i.email,
                   i.full_name,
                   i.role,
                   i.status,
                   i.assigned_port_id,
                   COALESCE(p.name, 'Tất cả') AS port_name,
                   i.last_login_at
            FROM inserted i
            LEFT JOIN operational.ports p ON p.id = i.assigned_port_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        AddUserWriteParameters(command, input.Email, input.FullName, input.Role, input.Status, input.PortId);
        command.Parameters.AddWithValue("passwordHash", input.PasswordHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadUserSummary(reader);
    }

    public async Task<UserSummaryReadModel?> UpdateUserAsync(
        Guid userId,
        UpdateUserReadModel input,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            WITH updated AS (
                UPDATE operational.users
                SET email = @email,
                    full_name = @fullName,
                    role = @role::operational.user_role_enum,
                    status = @status::operational.user_status_enum,
                    assigned_port_id = @portId,
                    updated_at = NOW()
                WHERE id = @userId
                  AND deleted_at IS NULL
                RETURNING id, email, full_name, role, status, assigned_port_id, last_login_at
            )
            SELECT u.id,
                   u.email,
                   u.full_name,
                   u.role,
                   u.status,
                   u.assigned_port_id,
                   COALESCE(p.name, 'Tất cả') AS port_name,
                   u.last_login_at
            FROM updated u
            LEFT JOIN operational.ports p ON p.id = u.assigned_port_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        AddUserWriteParameters(command, input.Email, input.FullName, input.Role, input.Status, input.PortId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadUserSummary(reader) : null;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE operational.users
            SET deleted_at = NOW(),
                updated_at = NOW()
            WHERE id = @userId
              AND deleted_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<AuthUserReadModel?> FindForAuthenticationAsync(
        string email,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            SELECT u.id, u.email, u.full_name, u.password_hash, u.role, u.status,
                   COALESCE(p.name, 'Tất cả cảng')
            FROM operational.users u
            LEFT JOIN operational.ports p ON p.id = u.assigned_port_id
            WHERE LOWER(u.email) = LOWER(@email) AND u.deleted_at IS NULL
            LIMIT 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAuthUser(reader) : null;
    }

    public async Task<AuthUserReadModel?> FindForAuthenticationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            SELECT u.id, u.email, u.full_name, u.password_hash, u.role, u.status,
                   COALESCE(p.name, 'Tất cả cảng')
            FROM operational.users u
            LEFT JOIN operational.ports p ON p.id = u.assigned_port_id
            WHERE u.id = @userId AND u.deleted_at IS NULL
            LIMIT 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAuthUser(reader) : null;
    }

    public async Task StoreRefreshTokenAsync(
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            UPDATE operational.users
            SET last_login_at = NOW(),
                failed_login_count = 0,
                updated_at = NOW()
            WHERE id = @userId;

            INSERT INTO operational.refresh_tokens (
                user_id,
                token_hash,
                expires_at
            )
            VALUES (
                @userId,
                @refreshTokenHash,
                @expiresAt
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("refreshTokenHash", refreshTokenHash);
        command.Parameters.AddWithValue("expiresAt", expiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            UPDATE operational.users
            SET password_hash = @passwordHash,
                password_changed_at = NOW(),
                updated_at = NOW()
            WHERE id = @userId;

            UPDATE operational.refresh_tokens
            SET revoked_at = NOW()
            WHERE user_id = @userId
              AND revoked_at IS NULL;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("passwordHash", passwordHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AuthUserReadModel ReadAuthUser(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6));

    private static UserSummaryReadModel ReadUserSummary(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));

    private static void AddUserWriteParameters(
        NpgsqlCommand command,
        string email,
        string fullName,
        string role,
        string status,
        Guid? portId)
    {
        command.Parameters.AddWithValue("email", email.Trim());
        command.Parameters.AddWithValue("fullName", fullName.Trim());
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.Add(new NpgsqlParameter("portId", NpgsqlDbType.Uuid)
        {
            Value = portId.HasValue ? portId.Value : DBNull.Value
        });
    }
}

public sealed record UserSummaryReadModel(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string Status,
    Guid? PortId,
    string PortName,
    DateTimeOffset? LastLoginAt);

public sealed record CreateUserReadModel(
    string Email,
    string FullName,
    string PasswordHash,
    string Role,
    string Status,
    Guid? PortId);

public sealed record UpdateUserReadModel(
    string Email,
    string FullName,
    string Role,
    string Status,
    Guid? PortId);

public sealed record AuthUserReadModel(
    Guid Id,
    string Email,
    string FullName,
    string PasswordHash,
    string Role,
    string Status,
    string PortName);
