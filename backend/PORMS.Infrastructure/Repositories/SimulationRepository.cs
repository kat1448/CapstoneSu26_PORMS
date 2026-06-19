using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class SimulationRepository
{
    private static readonly SimulationStep[] DemoSteps =
    [
        new(
            1,
            "LOW",
            "LOW",
            "LOW",
            "LOW",
            "WIND",
            "NORMAL",
            4.5m,
            3,
            1.0m,
            14.0m,
            "Stable harbor conditions"),
        new(
            2,
            "MEDIUM",
            "MEDIUM",
            "MEDIUM",
            "MEDIUM",
            "WIND",
            "NORMAL",
            10.8m,
            6,
            12.0m,
            8.0m,
            "Increasing wind and moderate rain"),
        new(
            3,
            "HIGH",
            "HIGH",
            "HIGH",
            "HIGH",
            "WIND",
            "LIMITED",
            18.2m,
            8,
            28.0m,
            4.0m,
            "Unsafe cargo handling conditions"),
        new(
            4,
            "CRITICAL",
            "CRITICAL",
            "CRITICAL",
            "CRITICAL",
            "WIND",
            "STOP",
            27.4m,
            10,
            60.0m,
            0.8m,
            "Operations must stop immediately")
    ];

    private readonly NpgsqlConnectionFactory _connectionFactory;

    public SimulationRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SimulationRunResult?> RunDemoAsync(string? portCode, CancellationToken cancellationToken)
    {
        var requestedPortCode = string.IsNullOrWhiteSpace(portCode) ? "DNTSA" : portCode.Trim();

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var port = await GetPortAsync(connection, transaction, requestedPortCode, cancellationToken);
        if (port is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var startedByUserId = await EnsureDemoUserAsync(connection, transaction, port.PortId, cancellationToken);
        var datasetId = await EnsureDemoDatasetAsync(connection, transaction, startedByUserId, cancellationToken);

        var sessionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        await InsertSimulationSessionAsync(
            connection,
            transaction,
            sessionId,
            datasetId,
            port.PortId,
            startedByUserId,
            startedAt,
            cancellationToken);

        await InsertOperationEventAsync(
            connection,
            transaction,
            Guid.NewGuid(),
            "SIMULATION_STARTED",
            port.PortId,
            startedByUserId,
            sessionId,
            $"Demo simulation started for port {port.PortCode}.",
            new
            {
                sessionId,
                portCode = port.PortCode,
                stepCount = DemoSteps.Length
            },
            startedAt,
            cancellationToken);

        var previousRiskLevel = port.CurrentRiskLevel;
        var previousMode = port.CurrentOperationMode;
        var generatedAlertCount = 0;
        var modeChangeCount = 0;

        foreach (var step in DemoSteps)
        {
            var observedAt = startedAt.AddMinutes(step.StepNumber * 5);
            var weatherReadingId = Guid.NewGuid();
            var riskAssessmentId = Guid.NewGuid();

            await InsertWeatherReadingAsync(
                connection,
                transaction,
                weatherReadingId,
                port.PortId,
                sessionId,
                step,
                observedAt,
                cancellationToken);

            await InsertRiskAssessmentAsync(
                connection,
                transaction,
                riskAssessmentId,
                weatherReadingId,
                port.PortId,
                sessionId,
                step,
                previousRiskLevel,
                observedAt,
                cancellationToken);

            if (!string.Equals(previousMode, step.OperationMode, StringComparison.Ordinal))
            {
                await InsertOperationModeLogAsync(
                    connection,
                    transaction,
                    Guid.NewGuid(),
                    port.PortId,
                    previousMode,
                    step.OperationMode,
                    riskAssessmentId,
                    startedByUserId,
                    sessionId,
                    observedAt,
                    cancellationToken);

                modeChangeCount++;
            }

            if (step.FinalRiskLevel is "HIGH" or "CRITICAL")
            {
                await InsertAlertAsync(
                    connection,
                    transaction,
                    Guid.NewGuid(),
                    port.PortId,
                    riskAssessmentId,
                    sessionId,
                    step,
                    observedAt,
                    cancellationToken);

                generatedAlertCount++;
            }

            await InsertOperationEventAsync(
                connection,
                transaction,
                Guid.NewGuid(),
                "SIMULATION_STEP",
                port.PortId,
                startedByUserId,
                sessionId,
                $"Simulation step {step.StepNumber} advanced port {port.PortCode} to {step.FinalRiskLevel}.",
                new
                {
                    sessionId,
                    step = step.StepNumber,
                    riskLevel = step.FinalRiskLevel,
                    operationMode = step.OperationMode,
                    windSpeedMs = step.WindSpeedMs,
                    beaufortNumber = step.BeaufortNumber
                },
                observedAt,
                cancellationToken);

            previousRiskLevel = step.FinalRiskLevel;
            previousMode = step.OperationMode;
        }

        var completedAt = startedAt.AddMinutes((DemoSteps.Length + 1) * 5);
        await InsertOperationEventAsync(
            connection,
            transaction,
            Guid.NewGuid(),
            "SIMULATION_COMPLETED",
            port.PortId,
            startedByUserId,
            sessionId,
            $"Demo simulation completed for port {port.PortCode}.",
            new
            {
                sessionId,
                peakRiskLevel = DemoSteps[^1].FinalRiskLevel,
                generatedAlertCount,
                modeChangeCount
            },
            completedAt,
            cancellationToken);

        await CompleteSimulationSessionAsync(
            connection,
            transaction,
            sessionId,
            DemoSteps[^1].FinalRiskLevel,
            generatedAlertCount,
            modeChangeCount,
            completedAt,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new SimulationRunResult(
            sessionId,
            port.PortId,
            port.PortCode,
            DemoSteps.Length,
            generatedAlertCount,
            modeChangeCount,
            DemoSteps[^1].FinalRiskLevel,
            DemoSteps[^1].OperationMode);
    }

    public async Task<SimulationSnapshotReadModel> GetCurrentAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT s.id,
                   s.status,
                   COALESCE(s.progress_percent, 0),
                   COALESCE(s.generated_alert_count, 0),
                   COALESCE(s.mode_change_count, 0),
                   p.current_risk_level,
                   p.current_operation_mode,
                   COALESCE(w.wind_speed_ms, 0),
                   COALESCE(w.beaufort_number, 0),
                   COALESCE(w.rainfall_1h_mm, 0),
                   COALESCE(w.visibility_km, 0)
            FROM operational.simulation_sessions s
            JOIN operational.ports p ON p.id = s.port_id
            LEFT JOIN LATERAL (
                SELECT *
                FROM operational.weather_readings wr
                WHERE wr.simulation_session_id = s.id
                ORDER BY wr.observed_at DESC
                LIMIT 1
            ) w ON TRUE
            ORDER BY s.created_at DESC
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new SimulationSnapshotReadModel(
                "IDLE",
                "LOW",
                "NORMAL",
                0,
                0,
                0,
                0,
                0,
                0,
                []);
        }

        var sessionId = reader.GetGuid(0);
        var snapshot = new SimulationSnapshotReadModel(
            reader.GetString(1),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetInt16(8),
            reader.GetDecimal(9),
            reader.GetDecimal(10),
            reader.GetDecimal(2),
            reader.GetInt32(3),
            []);
        var modeChangeCount = reader.GetInt32(4);

        await reader.DisposeAsync();

        var feed = await GetSimulationFeedAsync(connection, sessionId, cancellationToken);
        return snapshot with
        {
            ModeChangeCount = modeChangeCount,
            Feed = feed
        };
    }

    private static async Task<IReadOnlyList<SimulationFeedReadModel>> GetSimulationFeedAsync(
        NpgsqlConnection connection,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_type,
                   summary,
                   COALESCE(
                       payload ->> 'riskLevel',
                       payload ->> 'peakRiskLevel',
                       'LOW'
                   ) AS risk_level,
                   occurred_at
            FROM operational.operation_events
            WHERE simulation_session_id = @sessionId
            ORDER BY occurred_at DESC
            LIMIT 10;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SimulationFeedReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimulationFeedReadModel(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3).ToLocalTime().ToString("HH:mm:ss")));
        }

        return results;
    }

    private static async Task<PortContext?> GetPortAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string portCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id,
                   code,
                   current_risk_level,
                   current_operation_mode
            FROM operational.ports
            WHERE deleted_at IS NULL
              AND UPPER(code) = UPPER(@portCode)
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("portCode", portCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PortContext(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static async Task<Guid> EnsureDemoUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid portId,
        CancellationToken cancellationToken)
    {
        const string sql = """
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
                'demo.operator@porms.local',
                'PORMS Demo Operator',
                'demo-not-for-login',
                'OPERATOR',
                'ACTIVE',
                @portId,
                NOW()
            )
            ON CONFLICT ((LOWER(email))) WHERE deleted_at IS NULL
            DO UPDATE SET
                full_name = EXCLUDED.full_name,
                assigned_port_id = EXCLUDED.assigned_port_id,
                status = 'ACTIVE',
                deleted_at = NULL,
                updated_at = NOW()
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("portId", portId);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid userId
            ? userId
            : throw new InvalidOperationException("Failed to resolve demo simulation user.");
    }

    private static async Task<Guid> EnsureDemoDatasetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.simulation_datasets (
                name,
                description,
                checksum_sha256,
                snapshot_count,
                starts_at,
                ends_at,
                metadata,
                created_by_user_id
            )
            VALUES (
                'Deterministic Demo Progression',
                'Backend-generated LOW to CRITICAL demo sequence for dashboard walkthroughs.',
                'demo-low-medium-high-critical-v1',
                4,
                NOW(),
                NOW() + INTERVAL '20 minutes',
                '{"source":"backend-demo","kind":"deterministic"}'::jsonb,
                @createdByUserId
            )
            ON CONFLICT (checksum_sha256) WHERE checksum_sha256 IS NOT NULL
            DO UPDATE SET
                description = EXCLUDED.description,
                snapshot_count = EXCLUDED.snapshot_count,
                metadata = EXCLUDED.metadata,
                updated_at = NOW()
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("createdByUserId", createdByUserId);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid datasetId
            ? datasetId
            : throw new InvalidOperationException("Failed to resolve demo simulation dataset.");
    }

    private static async Task InsertSimulationSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        Guid datasetId,
        Guid portId,
        Guid startedByUserId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.simulation_sessions (
                id,
                dataset_id,
                port_id,
                started_by_user_id,
                status,
                speed_multiplier,
                progress_percent,
                current_snapshot_number,
                started_at
            )
            VALUES (
                @id,
                @datasetId,
                @portId,
                @startedByUserId,
                'RUNNING',
                1,
                0,
                0,
                @startedAt
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("datasetId", datasetId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("startedByUserId", startedByUserId);
        command.Parameters.AddWithValue("startedAt", startedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertWeatherReadingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid weatherReadingId,
        Guid portId,
        Guid sessionId,
        SimulationStep step,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.weather_readings (
                id,
                port_id,
                simulation_session_id,
                wind_speed_ms,
                beaufort_number,
                rainfall_1h_mm,
                visibility_km,
                weather_description,
                observed_at,
                data_source,
                source_record_key,
                raw_payload,
                is_simulation
            )
            VALUES (
                @id,
                @portId,
                @sessionId,
                @windSpeedMs,
                @beaufortNumber,
                @rainfall1hMm,
                @visibilityKm,
                @weatherDescription,
                @observedAt,
                'SIMULATION_DEMO',
                @sourceRecordKey,
                @rawPayload,
                TRUE
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", weatherReadingId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("windSpeedMs", step.WindSpeedMs);
        command.Parameters.AddWithValue("beaufortNumber", step.BeaufortNumber);
        command.Parameters.AddWithValue("rainfall1hMm", step.Rainfall1hMm);
        command.Parameters.AddWithValue("visibilityKm", step.VisibilityKm);
        command.Parameters.AddWithValue("weatherDescription", step.WeatherDescription);
        command.Parameters.AddWithValue("observedAt", observedAt);
        command.Parameters.AddWithValue("sourceRecordKey", $"{sessionId}:step-{step.StepNumber}");
        command.Parameters.Add("rawPayload", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(new
        {
            step = step.StepNumber,
            riskLevel = step.FinalRiskLevel,
            demo = true
        });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRiskAssessmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid riskAssessmentId,
        Guid weatherReadingId,
        Guid portId,
        Guid sessionId,
        SimulationStep step,
        string previousRiskLevel,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
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
                threshold_version,
                evaluated_at,
                is_simulation
            )
            VALUES (
                @id,
                @weatherReadingId,
                @portId,
                @sessionId,
                CAST(@windRiskLevel AS operational.risk_level_enum),
                CAST(@rainRiskLevel AS operational.risk_level_enum),
                CAST(@visibilityRiskLevel AS operational.risk_level_enum),
                CAST(@finalRiskLevel AS operational.risk_level_enum),
                CAST(@previousRiskLevel AS operational.risk_level_enum),
                @levelChanged,
                CAST(@dominantFactor AS operational.weather_factor_enum),
                @assessmentSummary,
                1,
                @evaluatedAt,
                TRUE
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", riskAssessmentId);
        command.Parameters.AddWithValue("weatherReadingId", weatherReadingId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("windRiskLevel", step.WindRiskLevel);
        command.Parameters.AddWithValue("rainRiskLevel", step.RainRiskLevel);
        command.Parameters.AddWithValue("visibilityRiskLevel", step.VisibilityRiskLevel);
        command.Parameters.AddWithValue("finalRiskLevel", step.FinalRiskLevel);
        command.Parameters.AddWithValue("previousRiskLevel", previousRiskLevel);
        command.Parameters.AddWithValue("levelChanged", !string.Equals(previousRiskLevel, step.FinalRiskLevel, StringComparison.Ordinal));
        command.Parameters.AddWithValue("dominantFactor", step.DominantFactor);
        command.Parameters.AddWithValue("assessmentSummary", step.WeatherDescription);
        command.Parameters.AddWithValue("evaluatedAt", observedAt.AddMinutes(1));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOperationModeLogAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationModeLogId,
        Guid portId,
        string previousMode,
        string newMode,
        Guid riskAssessmentId,
        Guid changedByUserId,
        Guid sessionId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.operation_mode_logs (
                id,
                port_id,
                previous_mode,
                new_mode,
                change_source,
                risk_assessment_id,
                changed_by_user_id,
                simulation_session_id,
                changed_at
            )
            VALUES (
                @id,
                @portId,
                CAST(@previousMode AS operational.operation_mode_enum),
                CAST(@newMode AS operational.operation_mode_enum),
                'SIMULATION',
                @riskAssessmentId,
                @changedByUserId,
                @sessionId,
                @changedAt
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", operationModeLogId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("previousMode", previousMode);
        command.Parameters.AddWithValue("newMode", newMode);
        command.Parameters.AddWithValue("riskAssessmentId", riskAssessmentId);
        command.Parameters.AddWithValue("changedByUserId", changedByUserId);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("changedAt", changedAt.AddMinutes(2));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAlertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid alertId,
        Guid portId,
        Guid riskAssessmentId,
        Guid sessionId,
        SimulationStep step,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.alerts (
                id,
                port_id,
                risk_assessment_id,
                alert_type,
                severity,
                title,
                message,
                context,
                expires_at,
                simulation_session_id,
                created_at,
                updated_at
            )
            VALUES (
                @id,
                @portId,
                @riskAssessmentId,
                'SIMULATION',
                CAST(@severity AS operational.alert_severity_enum),
                @title,
                @message,
                @context,
                @expiresAt,
                @sessionId,
                @createdAt,
                @createdAt
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", alertId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("riskAssessmentId", riskAssessmentId);
        command.Parameters.AddWithValue("severity", step.FinalRiskLevel);
        command.Parameters.AddWithValue("title", $"Simulation {step.FinalRiskLevel.ToLowerInvariant()} risk alert");
        command.Parameters.AddWithValue("message", $"Port conditions reached {step.FinalRiskLevel} during demo simulation.");
        command.Parameters.Add("context", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(new
        {
            step = step.StepNumber,
            riskLevel = step.FinalRiskLevel,
            operationMode = step.OperationMode
        });
        command.Parameters.AddWithValue("expiresAt", createdAt.AddHours(2));
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("createdAt", createdAt.AddMinutes(3));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOperationEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid operationEventId,
        string eventType,
        Guid portId,
        Guid actorUserId,
        Guid sessionId,
        string summary,
        object payload,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.operation_events (
                id,
                event_type,
                port_id,
                actor_user_id,
                entity_type,
                entity_id,
                summary,
                payload,
                simulation_session_id,
                occurred_at
            )
            VALUES (
                @id,
                @eventType,
                @portId,
                @actorUserId,
                'simulation_session',
                @entityId,
                @summary,
                @payload,
                @sessionId,
                @occurredAt
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", operationEventId);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("entityId", sessionId);
        command.Parameters.AddWithValue("summary", summary);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(payload);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteSimulationSessionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        string peakRiskLevel,
        int generatedAlertCount,
        int modeChangeCount,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE operational.simulation_sessions
            SET status = 'COMPLETED',
                progress_percent = 100,
                current_snapshot_number = 4,
                peak_risk_level = CAST(@peakRiskLevel AS operational.risk_level_enum),
                generated_alert_count = @generatedAlertCount,
                generated_task_count = 0,
                sop_execution_count = 0,
                mode_change_count = @modeChangeCount,
                ended_at = @completedAt,
                updated_at = NOW()
            WHERE id = @id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("peakRiskLevel", peakRiskLevel);
        command.Parameters.AddWithValue("generatedAlertCount", generatedAlertCount);
        command.Parameters.AddWithValue("modeChangeCount", modeChangeCount);
        command.Parameters.AddWithValue("completedAt", completedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record PortContext(
        Guid PortId,
        string PortCode,
        string CurrentRiskLevel,
        string CurrentOperationMode);

    private sealed record SimulationStep(
        int StepNumber,
        string WindRiskLevel,
        string RainRiskLevel,
        string VisibilityRiskLevel,
        string FinalRiskLevel,
        string DominantFactor,
        string OperationMode,
        decimal WindSpeedMs,
        short BeaufortNumber,
        decimal Rainfall1hMm,
        decimal VisibilityKm,
        string WeatherDescription);
}

public sealed record SimulationRunResult(
    Guid SessionId,
    Guid PortId,
    string PortCode,
    int StepCount,
    int GeneratedAlertCount,
    int ModeChangeCount,
    string FinalRiskLevel,
    string FinalOperationMode);

public sealed record SimulationSnapshotReadModel(
    string Status,
    string CurrentRiskLevel,
    string CurrentMode,
    decimal WindSpeedMs,
    short BeaufortNumber,
    decimal Rainfall1hMm,
    decimal VisibilityKm,
    decimal ProgressPercent,
    int GeneratedAlertCount,
    IReadOnlyList<SimulationFeedReadModel> Feed)
{
    public int ModeChangeCount { get; init; }
}

public sealed record SimulationFeedReadModel(
    string Title,
    string Detail,
    string RiskLevel,
    string HappenedAt);
