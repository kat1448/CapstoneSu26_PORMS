using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PORMS.Tests.Integration;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestDatabaseConnectionEnvironmentVariable = "PORMS_TEST_DB_CONNECTION";
    private const string FallbackConnectionString =
        "Host=porms_api;Port=5432;Database=porms_db;Username=postgres;Password=testpass";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.SetBasePath(AppContext.BaseDirectory);
            configurationBuilder.AddJsonFile("appsettings.Testing.json", optional: false);

            var connectionString = Environment.GetEnvironmentVariable(
                TestDatabaseConnectionEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return;
            }

            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:ConnectionString"] = connectionString
            });
        });
    }

    public string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            TestDatabaseConnectionEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Testing.json", optional: true)
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]
            ?? FallbackConnectionString;
    }

    public async Task<(Guid PortId, string PortCode)> GetPrimaryPortAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, code
            FROM operational.ports
            WHERE deleted_at IS NULL
            ORDER BY code
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("No active test port exists.");
        }

        return (reader.GetGuid(0), reader.GetString(1));
    }

    public async Task SeedAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.alerts (
                id,
                port_id,
                alert_type,
                severity,
                title,
                message,
                context,
                expires_at
            )
            VALUES (
                @id,
                @portId,
                'SYSTEM',
                'HIGH',
                'Seeded smoke alert',
                'Created by integration test.',
                '{"source":"integration-test"}'::jsonb,
                NOW() + INTERVAL '1 day'
            )
            ON CONFLICT (id) DO UPDATE
            SET port_id = EXCLUDED.port_id,
                event_type = EXCLUDED.event_type,
                entity_type = EXCLUDED.entity_type,
                summary = EXCLUDED.summary,
                payload = EXCLUDED.payload,
                occurred_at = EXCLUDED.occurred_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", alertId);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedOperationEventAsync(Guid operationEventId, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.operation_events (
                id,
                event_type,
                port_id,
                entity_type,
                summary,
                payload,
                occurred_at
            )
            VALUES (
                @id,
                'SYSTEM_TEST',
                @portId,
                'integration-test',
                'Smoke test event',
                '{"source":"integration-test"}'::jsonb,
                NOW()
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", operationEventId);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task HideAllPortsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE operational.ports
            SET deleted_at = NOW()
            WHERE deleted_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RestoreAllPortsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE operational.ports
            SET deleted_at = NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ResetPrimaryPortStateAsync(CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE operational.ports
            SET current_risk_level = 'LOW',
                current_operation_mode = 'NORMAL'
            WHERE id = @portId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SimulationSessionSnapshot> GetSimulationSessionSnapshotAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT s.id,
                   s.port_id,
                   s.status,
                   s.progress_percent,
                   s.current_snapshot_number,
                   s.peak_risk_level,
                   s.generated_alert_count,
                   s.mode_change_count,
                   p.current_risk_level,
                   p.current_operation_mode,
                   COALESCE((
                       SELECT COUNT(*)
                       FROM operational.weather_readings w
                       WHERE w.simulation_session_id = s.id
                   ), 0) AS weather_count,
                   COALESCE((
                       SELECT COUNT(*)
                       FROM operational.risk_assessments r
                       WHERE r.simulation_session_id = s.id
                   ), 0) AS risk_count,
                   COALESCE((
                       SELECT COUNT(*)
                       FROM operational.alerts a
                       WHERE a.simulation_session_id = s.id
                   ), 0) AS alert_count,
                   COALESCE((
                       SELECT COUNT(*)
                       FROM operational.operation_mode_logs m
                       WHERE m.simulation_session_id = s.id
                   ), 0) AS mode_log_count,
                   COALESCE((
                       SELECT COUNT(*)
                       FROM operational.operation_events e
                       WHERE e.simulation_session_id = s.id
                   ), 0) AS event_count
            FROM operational.simulation_sessions s
            JOIN operational.ports p ON p.id = s.port_id
            WHERE s.id = @sessionId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Simulation session {sessionId} was not found.");
        }

        return new SimulationSessionSnapshot(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetInt64(10),
            reader.GetInt64(11),
            reader.GetInt64(12),
            reader.GetInt64(13),
            reader.GetInt64(14));
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed record SimulationSessionSnapshot(
    Guid SessionId,
    Guid PortId,
    string Status,
    decimal ProgressPercent,
    int CurrentSnapshotNumber,
    string PeakRiskLevel,
    int GeneratedAlertCount,
    int ModeChangeCount,
    string CurrentRiskLevel,
    string CurrentOperationMode,
    long WeatherReadingCount,
    long RiskAssessmentCount,
    long AlertCount,
    long ModeLogCount,
    long OperationEventCount);
