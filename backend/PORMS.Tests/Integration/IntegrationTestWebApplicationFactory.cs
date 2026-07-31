using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PORMS.API.Services;

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
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<FakeTaskAssignmentEmailNotifier>();
            services.AddSingleton<ITaskAssignmentEmailNotifier>(sp =>
                sp.GetRequiredService<FakeTaskAssignmentEmailNotifier>());
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

    public async Task<(Guid ZoneId, string ZoneName)> GetFirstZoneAsync(Guid portId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, name
            FROM operational.zones
            WHERE port_id = @portId
              AND deleted_at IS NULL
            ORDER BY display_order, name
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("portId", portId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"No active test zone exists for port {portId}.");
        }

        return (reader.GetGuid(0), reader.GetString(1));
    }

    public async Task<(Guid UserId, string FullName, string Email)> GetFirstActiveUserAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, full_name, email
            FROM operational.users
            WHERE deleted_at IS NULL
              AND status = 'ACTIVE'
            ORDER BY created_at, full_name
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("No active test user exists.");
        }

        return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task<(Guid UserId, string FullName, string Email)> GetFirstActiveUserByRoleAsync(
        string role,
        Guid? assignedPortId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, full_name, email
            FROM operational.users
            WHERE deleted_at IS NULL
              AND status = 'ACTIVE'
              AND role::text = @role
              AND (@assignedPortId IS NULL OR assigned_port_id = @assignedPortId)
            ORDER BY created_at, full_name
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("role", role);
        command.Parameters.Add(new NpgsqlParameter("assignedPortId", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = assignedPortId.HasValue ? assignedPortId.Value : DBNull.Value
        });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"No active {role} test user exists.");
        }

        return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
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
                expires_at,
                created_at,
                updated_at
            )
            VALUES (
                @id,
                @portId,
                'SYSTEM',
                'HIGH',
                'Seeded smoke alert',
                'Created by integration test.',
                '{"source":"integration-test"}'::jsonb,
                NOW() + INTERVAL '1 day',
                NOW(),
                NOW()
            )
            ON CONFLICT (id) DO UPDATE
            SET port_id = EXCLUDED.port_id,
                alert_type = EXCLUDED.alert_type,
                severity = EXCLUDED.severity,
                title = EXCLUDED.title,
                message = EXCLUDED.message,
                context = EXCLUDED.context,
                expires_at = EXCLUDED.expires_at,
                created_at = EXCLUDED.created_at,
                updated_at = NOW();
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

    public async Task SeedTaskAsync(string taskCode, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        var zone = await GetFirstZoneAsync(port.PortId, cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.tasks (
                task_code,
                port_id,
                zone_id,
                title,
                description,
                priority,
                status,
                assigned_team,
                created_at,
                updated_at
            )
            VALUES (
                @taskCode,
                @portId,
                @zoneId,
                'Seeded integration task',
                'Created by integration test.',
                'HIGH',
                'NEW',
                'Đội vận hành',
                NOW(),
                NOW()
            )
            ON CONFLICT (task_code) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskCode", taskCode);
        command.Parameters.AddWithValue("portId", port.PortId);
        command.Parameters.AddWithValue("zoneId", zone.ZoneId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Guid> SeedAlertTaskAsync(Guid alertId, string taskCode, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        var zone = await GetFirstZoneAsync(port.PortId, cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.alerts (
                id,
                port_id,
                zone_id,
                alert_type,
                severity,
                title,
                message,
                context,
                expires_at,
                created_at,
                updated_at
            )
            VALUES (
                @alertId,
                @portId,
                @zoneId,
                'SYSTEM',
                'HIGH',
                'Seeded alert workflow',
                'Created by alert task integration test.',
                '{"source":"integration-test"}'::jsonb,
                NOW() + INTERVAL '1 day',
                NOW(),
                NOW()
            )
            ON CONFLICT (id) DO UPDATE
            SET port_id = EXCLUDED.port_id,
                zone_id = EXCLUDED.zone_id,
                updated_at = NOW();

            INSERT INTO operational.tasks (
                task_code,
                alert_id,
                port_id,
                zone_id,
                title,
                description,
                priority,
                status,
                created_at,
                updated_at
            )
            VALUES (
                @taskCode,
                @alertId,
                @portId,
                @zoneId,
                'Alert workflow integration task',
                'Created by integration test.',
                'HIGH',
                'NEW',
                NOW(),
                NOW()
            )
            ON CONFLICT (task_code) DO UPDATE
            SET alert_id = EXCLUDED.alert_id,
                port_id = EXCLUDED.port_id,
                zone_id = EXCLUDED.zone_id,
                status = 'NEW',
                assigned_user_id = NULL,
                assigned_team = NULL,
                acknowledged_by_user_id = NULL,
                acknowledged_at = NULL,
                started_at = NULL,
                completed_by_user_id = NULL,
                completed_at = NULL,
                completion_note = NULL,
                updated_at = NOW()
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("alertId", alertId);
        command.Parameters.AddWithValue("taskCode", taskCode);
        command.Parameters.AddWithValue("portId", port.PortId);
        command.Parameters.AddWithValue("zoneId", zone.ZoneId);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to seed alert task."));
    }

    public async Task DeleteTaskByCodeAsync(string taskCode, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            DELETE FROM operational.tasks
            WHERE task_code = @taskCode;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskCode", taskCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAlertAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            DELETE FROM operational.alert_receipts
            WHERE alert_id = @alertId;

            DELETE FROM operational.alerts
            WHERE id = @alertId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("alertId", alertId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedSimulationOperationEventAsync(Guid operationEventId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.simulation_datasets (
                id,
                name,
                description,
                snapshot_count
            )
            VALUES (
                @datasetId,
                'Operation log simulation dataset',
                'Integration test dataset',
                1
            )
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO operational.simulation_sessions (
                id,
                dataset_id,
                port_id,
                started_by_user_id,
                status,
                progress_percent,
                current_snapshot_number,
                peak_risk_level,
                started_at
            )
            SELECT
                @sessionId,
                @datasetId,
                @portId,
                id,
                'COMPLETED',
                100,
                1,
                'CRITICAL',
                NOW()
            FROM operational.users
            ORDER BY created_at
            LIMIT 1
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO operational.operation_events (
                id,
                event_type,
                port_id,
                entity_type,
                summary,
                payload,
                simulation_session_id,
                occurred_at
            )
            VALUES (
                @id,
                'SIMULATION_STEP',
                @portId,
                'simulation',
                'Simulation smoke event',
                '{"source":"integration-test"}'::jsonb,
                @sessionId,
                NOW()
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", operationEventId);
        command.Parameters.AddWithValue("datasetId", Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1"));
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedForecastWeatherAsync(Guid weatherReadingId, CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO operational.weather_readings (
                id,
                port_id,
                wind_speed_ms,
                beaufort_number,
                rainfall_1h_mm,
                temperature_c,
                humidity_pct,
                visibility_km,
                weather_description,
                observed_at,
                recorded_at,
                data_source,
                source_record_key,
                raw_payload,
                is_simulation
            )
            VALUES (
                @id,
                @portId,
                8.5,
                5,
                6,
                28,
                74,
                12,
                'Forecast planning seed',
                NOW(),
                NOW(),
                'OPENWEATHER_API',
                @sourceRecordKey,
                '{"source":"integration-test"}'::jsonb,
                FALSE
            )
            ON CONFLICT (id) DO UPDATE
            SET port_id = EXCLUDED.port_id,
                observed_at = EXCLUDED.observed_at,
                recorded_at = EXCLUDED.recorded_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", weatherReadingId);
        command.Parameters.AddWithValue("portId", port.PortId);
        command.Parameters.AddWithValue("sourceRecordKey", $"forecast-plan-test:{weatherReadingId}");
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

            UPDATE operational.zones
            SET current_risk_level = 'LOW',
                is_restricted = FALSE,
                restriction_reason = NULL
            WHERE port_id = @portId
              AND deleted_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SeedDashboardRiskIsolationAsync(CancellationToken cancellationToken = default)
    {
        var port = await GetPrimaryPortAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            DELETE FROM operational.risk_assessments
                WHERE id IN (
                    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1'::uuid,
                    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'::uuid
                );
                DELETE FROM operational.weather_readings
                WHERE id IN (
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1'::uuid,
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2'::uuid
                );
                DELETE FROM operational.simulation_sessions
                WHERE id = 'cccccccc-cccc-cccc-cccc-ccccccccccc1'::uuid;
                DELETE FROM operational.simulation_datasets
                WHERE id = 'dddddddd-dddd-dddd-dddd-ddddddddddd1'::uuid;

            INSERT INTO operational.simulation_datasets (
                    id,
                    name,
                    description,
                    snapshot_count
                )
                VALUES (
                    'dddddddd-dddd-dddd-dddd-ddddddddddd1'::uuid,
                    'Dashboard risk isolation',
                    'Integration test dataset',
                    1
                );

            INSERT INTO operational.simulation_sessions (
                    id,
                    dataset_id,
                    port_id,
                    started_by_user_id,
                    status,
                    progress_percent,
                    current_snapshot_number,
                    peak_risk_level,
                    started_at
                )
                SELECT
                    'cccccccc-cccc-cccc-cccc-ccccccccccc1'::uuid,
                    'dddddddd-dddd-dddd-dddd-ddddddddddd1'::uuid,
                    @portId,
                    id,
                    'COMPLETED',
                    100,
                    1,
                    'CRITICAL',
                    NOW()
                FROM operational.users
                ORDER BY created_at
                LIMIT 1;

            INSERT INTO operational.weather_readings (
                    id,
                    port_id,
                    wind_speed_ms,
                    beaufort_number,
                    rainfall_1h_mm,
                    temperature_c,
                    humidity_pct,
                    visibility_km,
                    weather_description,
                    observed_at,
                    recorded_at,
                    data_source,
                    is_simulation
                )
                VALUES (
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1'::uuid,
                    @portId,
                    2.5,
                    2,
                    0,
                    27,
                    70,
                    10,
                    'OpenWeather low risk',
                    NOW() + INTERVAL '10 minutes',
                    NOW() + INTERVAL '11 minutes',
                    'OPENWEATHER_API',
                    FALSE
                );

            INSERT INTO operational.risk_assessments (
                    id,
                    weather_reading_id,
                    port_id,
                    wind_risk_level,
                    rain_risk_level,
                    visibility_risk_level,
                    final_risk_level,
                    previous_risk_level,
                    level_changed,
                    dominant_factor,
                    assessment_summary,
                    evaluated_at,
                    is_simulation
                )
                VALUES (
                    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1'::uuid,
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1'::uuid,
                    @portId,
                    'LOW',
                    'LOW',
                    'LOW',
                    'LOW',
                    'LOW',
                    FALSE,
                    'WIND',
                    'OpenWeather should drive dashboard risk.',
                    NOW() + INTERVAL '12 minutes',
                    FALSE
                );

            INSERT INTO operational.weather_readings (
                    id,
                    port_id,
                    simulation_session_id,
                    wind_speed_ms,
                    beaufort_number,
                    rainfall_1h_mm,
                    temperature_c,
                    humidity_pct,
                    visibility_km,
                    weather_description,
                    observed_at,
                    recorded_at,
                    data_source,
                    is_simulation
                )
                VALUES (
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2'::uuid,
                    @portId,
                    'cccccccc-cccc-cccc-cccc-ccccccccccc1'::uuid,
                    27.4,
                    10,
                    60,
                    27,
                    90,
                    0.8,
                    'Simulation critical risk',
                    NOW() + INTERVAL '20 minutes',
                    NOW() + INTERVAL '21 minutes',
                    'SIMULATION_DEMO',
                    TRUE
                );

            INSERT INTO operational.risk_assessments (
                    id,
                    weather_reading_id,
                    port_id,
                    simulation_session_id,
                    wind_risk_level,
                    rain_risk_level,
                    visibility_risk_level,
                    final_risk_level,
                    previous_risk_level,
                    level_changed,
                    dominant_factor,
                    assessment_summary,
                    evaluated_at,
                    is_simulation
                )
                VALUES (
                    'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'::uuid,
                    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2'::uuid,
                    @portId,
                    'cccccccc-cccc-cccc-cccc-ccccccccccc1'::uuid,
                    'CRITICAL',
                    'CRITICAL',
                    'CRITICAL',
                    'CRITICAL',
                    'LOW',
                    TRUE,
                    'WIND',
                    'Simulation must not drive dashboard risk.',
                    NOW() + INTERVAL '22 minutes',
                    TRUE
                );

            UPDATE operational.ports
                SET current_risk_level = 'CRITICAL',
                    current_operation_mode = 'STOP'
                WHERE id = @portId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("portId", port.PortId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CleanupDashboardRiskIsolationAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            DELETE FROM operational.risk_assessments
            WHERE id IN (
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1'::uuid,
                'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'::uuid
            );
            DELETE FROM operational.weather_readings
            WHERE id IN (
                'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1'::uuid,
                'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2'::uuid
            );
            DELETE FROM operational.simulation_sessions
            WHERE id = 'cccccccc-cccc-cccc-cccc-ccccccccccc1'::uuid;
            DELETE FROM operational.simulation_datasets
            WHERE id = 'dddddddd-dddd-dddd-dddd-ddddddddddd1'::uuid;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await ResetPrimaryPortStateAsync(cancellationToken);
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
                   COALESCE(s.peak_risk_level::text, 'LOW') AS current_risk_level,
                   COALESCE((
                       SELECT m.new_mode::text
                       FROM operational.operation_mode_logs m
                       WHERE m.simulation_session_id = s.id
                       ORDER BY m.changed_at DESC
                       LIMIT 1
                   ), CASE s.peak_risk_level
                       WHEN 'CRITICAL' THEN 'STOP'
                       WHEN 'HIGH' THEN 'LIMITED'
                       ELSE 'NORMAL'
                   END) AS current_operation_mode,
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

    public async Task<AlertSnapshot> GetLatestAlertForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id,
                   zone_id,
                   title,
                   message,
                   created_at
            FROM operational.alerts
            WHERE simulation_session_id = @sessionId
            ORDER BY created_at DESC
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"No alert exists for simulation session {sessionId}.");
        }

        return new AlertSnapshot(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4));
    }

    public async Task<(Guid ZoneId, string ZoneName)> CreateTemporaryZoneWithWindOverrideAsync(
    Guid portId,
    decimal mediumWindThreshold,
    CancellationToken cancellationToken = default)
    {
        var zoneId = Guid.NewGuid();
        var zoneName = $"Integration Test Zone {zoneId:N}";

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string insertZoneSql = """
        INSERT INTO operational.zones (
            id,
            port_id,
            name,
            zone_type,
            description,
            display_order,
            is_active
        )
        VALUES (
            @zoneId,
            @portId,
            @zoneName,
            'YARD'::operational.zone_type_enum,
            'Khu vực tạm thời dùng cho integration test.',
            32767,
            TRUE
        );
        """;

        await using (var zoneCommand = new NpgsqlCommand(
            insertZoneSql,
            connection,
            transaction))
        {
            zoneCommand.Parameters.AddWithValue("zoneId", zoneId);
            zoneCommand.Parameters.AddWithValue("portId", portId);
            zoneCommand.Parameters.AddWithValue("zoneName", zoneName);

            await zoneCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertOverrideSql = """
        INSERT INTO operational.zone_threshold_overrides (
            id,
            zone_id,
            factor,
            risk_level,
            comparison_operator,
            threshold_value,
            unit,
            is_enabled,
            change_reason
        )
        VALUES (
            @id,
            @zoneId,
            'WIND'::operational.weather_factor_enum,
            'MEDIUM'::operational.risk_level_enum,
            'GTE'::operational.threshold_operator_enum,
            @thresholdValue,
            'Beaufort',
            TRUE,
            'Integration test: xác minh threshold riêng của khu vực.'
        );
        """;

        await using (var overrideCommand = new NpgsqlCommand(
            insertOverrideSql,
            connection,
            transaction))
        {
            overrideCommand.Parameters.AddWithValue("id", Guid.NewGuid());
            overrideCommand.Parameters.AddWithValue("zoneId", zoneId);
            overrideCommand.Parameters.AddWithValue(
                "thresholdValue",
                mediumWindThreshold);

            await overrideCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return (zoneId, zoneName);
    }

    public async Task DeleteTemporaryZoneAsync(
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        const string sql = """
        DELETE FROM operational.zones
        WHERE id = @zoneId;
        """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("zoneId", zoneId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(Guid DatasetId, string DatasetName)>
    SeedFutureForecastEvaluationDatasetAsync(
        string portCode,
        CancellationToken cancellationToken = default)
    {
        var datasetId = Guid.NewGuid();
        var datasetName = $"Forecast threshold test {datasetId:N}";
        var plannedAt = DateTimeOffset.UtcNow.AddDays(30);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        const string datasetSql = """
        INSERT INTO operational.simulation_datasets (
            id,
            name,
            description,
            snapshot_count,
            starts_at,
            ends_at,
            metadata
        )
        VALUES (
            @datasetId,
            @datasetName,
            'Dữ liệu tạm dùng để kiểm tra forecast evaluation.',
            1,
            @plannedAt,
            @plannedAt,
            jsonb_build_object(
                'source', 'forecast-plan',
                'portCode', @portCode
            )
        );
        """;

        await using (var command = new NpgsqlCommand(
            datasetSql,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("datasetId", datasetId);
            command.Parameters.AddWithValue("datasetName", datasetName);
            command.Parameters.AddWithValue(
                "plannedAt",
                plannedAt);
            command.Parameters.AddWithValue(
                "portCode",
                portCode.Trim().ToUpperInvariant());

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string snapshotSql = """
        INSERT INTO operational.simulation_snapshots (
            dataset_id,
            snapshot_number,
            source_observed_at,
            wind_speed_ms,
            beaufort_number,
            rainfall_1h_mm,
            visibility_km,
            raw_payload
        )
        VALUES (
            @datasetId,
            1,
            @plannedAt,
            4.5,
            3,
            0,
            1.2,
            '{"source":"integration-test"}'::jsonb
        );
        """;

        await using (var command = new NpgsqlCommand(
            snapshotSql,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("datasetId", datasetId);
            command.Parameters.AddWithValue("plannedAt", plannedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return (datasetId, datasetName);
    }

    public async Task DeleteForecastEvaluationDatasetAsync(
        Guid datasetId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        // Snapshot được tự động xóa theo ON DELETE CASCADE.
        const string sql = """
        DELETE FROM operational.simulation_datasets
        WHERE id = @datasetId;
        """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("datasetId", datasetId);

        await command.ExecuteNonQueryAsync(cancellationToken);
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

public sealed record AlertSnapshot(
    Guid AlertId,
    Guid? ZoneId,
    string Title,
    string Message,
    DateTimeOffset CreatedAt);
