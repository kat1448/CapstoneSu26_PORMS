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

    public async Task<IReadOnlyList<SimulationDatasetSummaryReadModel>> GetDatasetsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            SELECT id,
                   name,
                   description,
                   COALESCE(metadata ->> 'portCode', 'DNTSA') AS port_code,
                   snapshot_count
            FROM operational.simulation_datasets
            WHERE is_active = TRUE
            ORDER BY updated_at DESC, created_at DESC
            LIMIT 50;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SimulationDatasetSummaryReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimulationDatasetSummaryReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return results;
    }

    public async Task<IReadOnlyList<SimulationMapPointReadModel>> GetMapPointsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            SELECT z.id,
                   CONCAT(p.code, ' - ', z.name) AS zone_name,
                   COALESCE(z.latitude, p.latitude, 16.0678) AS latitude,
                   COALESCE(z.longitude, p.longitude, 108.2208) AS longitude,
                   z.current_risk_level::text
            FROM operational.zones z
            JOIN operational.ports p ON p.id = z.port_id
            WHERE z.deleted_at IS NULL
              AND p.deleted_at IS NULL
              AND z.is_active = TRUE
            ORDER BY p.code, z.display_order, z.name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SimulationMapPointReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimulationMapPointReadModel(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetDecimal(3),
                reader.GetString(4)));
        }

        return results;
    }

    public async Task<SimulationDatasetSummaryReadModel> CreateDatasetAsync(
        CreateSimulationDatasetReadModel input,
        CancellationToken cancellationToken)
    {
        var requestedPortCode = input.PortCode.Trim().ToUpperInvariant();
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var port = await GetPortAsync(connection, transaction, requestedPortCode, cancellationToken)
            ?? throw new InvalidOperationException($"Port {requestedPortCode} was not found.");
        var userId = await EnsureDemoUserAsync(connection, transaction, port.PortId, cancellationToken);
        var datasetId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var orderedSnapshots = input.Snapshots.OrderBy(item => item.SnapshotNumber).ToList();

        const string datasetSql = """
            INSERT INTO operational.simulation_datasets (
                id,
                name,
                description,
                snapshot_count,
                starts_at,
                ends_at,
                metadata,
                created_by_user_id
            )
            VALUES (
                @id,
                @name,
                @description,
                @snapshotCount,
                @startsAt,
                @endsAt,
                @metadata,
                @createdByUserId
            );
            """;

        await using (var command = new NpgsqlCommand(datasetSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", datasetId);
            command.Parameters.AddWithValue("name", input.Name.Trim());
            command.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(input.Description) ? DBNull.Value : input.Description.Trim());
            command.Parameters.AddWithValue("snapshotCount", orderedSnapshots.Count);
            command.Parameters.AddWithValue("startsAt", now);
            command.Parameters.AddWithValue("endsAt", now.AddMinutes(Math.Max(orderedSnapshots.Count, 1) * 5));
            command.Parameters.Add("metadata", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(new
            {
                portCode = port.PortCode,
                source = "manual-entry"
            });
            command.Parameters.AddWithValue("createdByUserId", userId);
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
                @snapshotNumber,
                @sourceObservedAt,
                @windSpeedMs,
                @beaufortNumber,
                @rainfall1hMm,
                @visibilityKm,
                @rawPayload
            );
            """;

        foreach (var snapshot in orderedSnapshots)
        {
            await using var command = new NpgsqlCommand(snapshotSql, connection, transaction);
            command.Parameters.AddWithValue("datasetId", datasetId);
            command.Parameters.AddWithValue("snapshotNumber", snapshot.SnapshotNumber);
            command.Parameters.AddWithValue("sourceObservedAt", now.AddMinutes(snapshot.SnapshotNumber * 5));
            command.Parameters.AddWithValue("windSpeedMs", snapshot.WindSpeedMs);
            command.Parameters.AddWithValue("beaufortNumber", snapshot.BeaufortNumber);
            command.Parameters.AddWithValue("rainfall1hMm", snapshot.Rainfall1hMm);
            command.Parameters.AddWithValue("visibilityKm", snapshot.VisibilityKm.HasValue ? snapshot.VisibilityKm.Value : DBNull.Value);
            command.Parameters.Add("rawPayload", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(new
            {
                zoneId = snapshot.ZoneId,
                source = "manual-entry"
            });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SimulationDatasetSummaryReadModel(datasetId, input.Name.Trim(), input.Description, port.PortCode, orderedSnapshots.Count);
    }

    public async Task<SimulationRunResult?> RunDatasetAsync(Guid datasetId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var dataset = await GetDatasetContextAsync(connection, transaction, datasetId, cancellationToken);
        if (dataset is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var port = await GetPortAsync(connection, transaction, dataset.PortCode, cancellationToken);
        if (port is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var snapshots = await GetDatasetSnapshotsAsync(connection, transaction, datasetId, cancellationToken);
        if (snapshots.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var firstZone = await GetFirstZoneAsync(connection, transaction, port.PortId, cancellationToken);
        var startedByUserId = await EnsureDemoUserAsync(connection, transaction, port.PortId, cancellationToken);
        var sessionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        await InsertSimulationSessionAsync(connection, transaction, sessionId, datasetId, port.PortId, startedByUserId, startedAt, cancellationToken);
        await InsertOperationEventAsync(connection, transaction, Guid.NewGuid(), "SIMULATION_STARTED", port.PortId, startedByUserId, sessionId, $"Simulation dataset {dataset.Name} started.", new { sessionId, datasetId }, startedAt, cancellationToken);

        var previousRiskLevel = port.CurrentRiskLevel;
        var previousMode = port.CurrentOperationMode;
        var generatedAlertCount = 0;
        var generatedTaskCount = 0;
        var modeChangeCount = 0;
        var peakRiskLevel = "LOW";

        foreach (var snapshot in snapshots)
        {
            var zone = await GetZoneAsync(connection, transaction, snapshot.ZoneId ?? firstZone?.ZoneId, cancellationToken);
            var risk = EvaluateRisk(snapshot);
            var operationMode = risk.FinalRiskLevel switch
            {
                "CRITICAL" => "STOP",
                "HIGH" => "LIMITED",
                _ => "NORMAL"
            };
            var observedAt = startedAt.AddMinutes(snapshot.SnapshotNumber * 5);
            var weatherReadingId = Guid.NewGuid();
            var riskAssessmentId = Guid.NewGuid();

            await InsertDatasetWeatherReadingAsync(connection, transaction, weatherReadingId, port.PortId, zone?.ZoneId, sessionId, snapshot, observedAt, cancellationToken);
            await InsertDatasetRiskAssessmentAsync(connection, transaction, riskAssessmentId, weatherReadingId, port.PortId, zone?.ZoneId, sessionId, risk, previousRiskLevel, observedAt, cancellationToken);

            if (!string.Equals(previousMode, operationMode, StringComparison.Ordinal))
            {
                await InsertOperationModeLogAsync(connection, transaction, Guid.NewGuid(), port.PortId, previousMode, operationMode, riskAssessmentId, startedByUserId, sessionId, observedAt, cancellationToken);
                modeChangeCount++;
            }

            if (risk.FinalRiskLevel is "HIGH" or "CRITICAL")
            {
                await InsertAlertAsync(connection, transaction, Guid.NewGuid(), port.PortId, riskAssessmentId, sessionId, new SimulationStep(snapshot.SnapshotNumber, risk.WindRiskLevel, risk.RainRiskLevel, risk.VisibilityRiskLevel, risk.FinalRiskLevel, risk.DominantFactor, operationMode, snapshot.WindSpeedMs, snapshot.BeaufortNumber, snapshot.Rainfall1hMm, snapshot.VisibilityKm ?? 0, risk.Summary), observedAt, cancellationToken);
                generatedAlertCount++;
                generatedTaskCount += await InsertSimulationTasksAsync(connection, transaction, port.PortId, zone, sessionId, riskAssessmentId, risk.FinalRiskLevel, cancellationToken);
            }

            await InsertOperationEventAsync(connection, transaction, Guid.NewGuid(), "SIMULATION_STEP", port.PortId, startedByUserId, sessionId, $"Simulation step {snapshot.SnapshotNumber} moved {zone?.ZoneName ?? port.PortCode} to {risk.FinalRiskLevel}.", new
            {
                sessionId,
                step = snapshot.SnapshotNumber,
                riskLevel = risk.FinalRiskLevel,
                zoneId = zone?.ZoneId,
                zoneName = zone?.ZoneName,
                windSpeedMs = snapshot.WindSpeedMs,
                beaufortNumber = snapshot.BeaufortNumber
            }, observedAt, cancellationToken);

            previousRiskLevel = risk.FinalRiskLevel;
            previousMode = operationMode;
            peakRiskLevel = HigherRisk(peakRiskLevel, risk.FinalRiskLevel);
        }

        var completedAt = startedAt.AddMinutes((snapshots.Count + 1) * 5);
        await InsertOperationEventAsync(connection, transaction, Guid.NewGuid(), "SIMULATION_COMPLETED", port.PortId, startedByUserId, sessionId, $"Simulation dataset {dataset.Name} completed.", new { sessionId, peakRiskLevel, generatedAlertCount, generatedTaskCount, modeChangeCount }, completedAt, cancellationToken);
        await CompleteSimulationSessionAsync(connection, transaction, sessionId, peakRiskLevel, generatedAlertCount, modeChangeCount, completedAt, cancellationToken);
        await UpdateGeneratedTaskCountAsync(connection, transaction, sessionId, generatedTaskCount, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new SimulationRunResult(sessionId, port.PortId, port.PortCode, snapshots.Count, generatedAlertCount, modeChangeCount, peakRiskLevel, previousMode)
        {
            GeneratedTaskCount = generatedTaskCount
        };
    }

    public async Task<SimulationResultReadModel?> GetResultAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string existsSql = "SELECT EXISTS (SELECT 1 FROM operational.simulation_sessions WHERE id = @sessionId);";
        await using (var existsCommand = new NpgsqlCommand(existsSql, connection))
        {
            existsCommand.Parameters.AddWithValue("sessionId", sessionId);
            if (await existsCommand.ExecuteScalarAsync(cancellationToken) is not bool exists || !exists)
            {
                return null;
            }
        }

        var mapPoints = new List<SimulationMapPointReadModel>();
        const string mapSql = """
            SELECT DISTINCT ON (z.id)
                   z.id,
                   z.name,
                   COALESCE(z.latitude, p.latitude, 16.0678),
                   COALESCE(z.longitude, p.longitude, 108.2208),
                   r.final_risk_level::text
            FROM operational.risk_assessments r
            JOIN operational.zones z ON z.id = r.zone_id
            JOIN operational.ports p ON p.id = r.port_id
            WHERE r.simulation_session_id = @sessionId
            ORDER BY z.id, r.evaluated_at DESC;
            """;
        await using (var command = new NpgsqlCommand(mapSql, connection))
        {
            command.Parameters.AddWithValue("sessionId", sessionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                mapPoints.Add(new SimulationMapPointReadModel(reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetString(4)));
            }
        }

        var dangerousZones = new List<SimulationDangerousZoneReadModel>();
        const string dangerousSql = """
            SELECT DISTINCT ON (z.id)
                   z.id,
                   z.name,
                   r.final_risk_level::text,
                   r.assessment_summary
            FROM operational.risk_assessments r
            JOIN operational.zones z ON z.id = r.zone_id
            WHERE r.simulation_session_id = @sessionId
              AND r.final_risk_level IN ('HIGH', 'CRITICAL')
            ORDER BY z.id, r.evaluated_at DESC;
            """;
        await using (var command = new NpgsqlCommand(dangerousSql, connection))
        {
            command.Parameters.AddWithValue("sessionId", sessionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dangerousZones.Add(new SimulationDangerousZoneReadModel(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        var tasks = new List<SimulationGeneratedTaskReadModel>();
        const string taskSql = """
            SELECT t.task_code,
                   t.title,
                   t.priority::text,
                   z.name
            FROM operational.tasks t
            LEFT JOIN operational.zones z ON z.id = t.zone_id
            WHERE t.simulation_session_id = @sessionId
            ORDER BY t.created_at DESC;
            """;
        await using (var command = new NpgsqlCommand(taskSql, connection))
        {
            command.Parameters.AddWithValue("sessionId", sessionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tasks.Add(new SimulationGeneratedTaskReadModel(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        return new SimulationResultReadModel(sessionId, mapPoints, dangerousZones, tasks);
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

    private static async Task<DatasetContext?> GetDatasetContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id,
                   name,
                   COALESCE(metadata ->> 'portCode', 'DNTSA') AS port_code
            FROM operational.simulation_datasets
            WHERE id = @datasetId
              AND is_active = TRUE;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("datasetId", datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DatasetContext(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    private static async Task<IReadOnlyList<SimulationSnapshotContext>> GetDatasetSnapshotsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT snapshot_number,
                   wind_speed_ms,
                   beaufort_number,
                   rainfall_1h_mm,
                   visibility_km,
                   NULLIF(raw_payload ->> 'zoneId', '')::uuid
            FROM operational.simulation_snapshots
            WHERE dataset_id = @datasetId
            ORDER BY snapshot_number;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("datasetId", datasetId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SimulationSnapshotContext>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimulationSnapshotContext(
                reader.GetInt32(0),
                reader.GetDecimal(1),
                reader.GetInt16(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5)));
        }

        return results;
    }

    private static async Task<ZoneContext?> GetFirstZoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid portId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, zone_type::text, latitude, longitude
            FROM operational.zones
            WHERE port_id = @portId
              AND deleted_at IS NULL
            ORDER BY display_order, name
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("portId", portId);
        return await ReadZoneAsync(command, cancellationToken);
    }

    private static async Task<ZoneContext?> GetZoneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? zoneId,
        CancellationToken cancellationToken)
    {
        if (!zoneId.HasValue)
        {
            return null;
        }

        const string sql = """
            SELECT id, name, zone_type::text, latitude, longitude
            FROM operational.zones
            WHERE id = @zoneId
              AND deleted_at IS NULL
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("zoneId", zoneId.Value);
        return await ReadZoneAsync(command, cancellationToken);
    }

    private static async Task<ZoneContext?> ReadZoneAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ZoneContext(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            reader.IsDBNull(4) ? null : reader.GetDecimal(4));
    }

    private static SimulationRiskContext EvaluateRisk(SimulationSnapshotContext snapshot)
    {
        var windRisk = snapshot.BeaufortNumber >= 10 ? "CRITICAL" : snapshot.BeaufortNumber >= 8 ? "HIGH" : snapshot.BeaufortNumber >= 6 ? "MEDIUM" : "LOW";
        var rainRisk = snapshot.Rainfall1hMm >= 50 ? "CRITICAL" : snapshot.Rainfall1hMm >= 25 ? "HIGH" : snapshot.Rainfall1hMm >= 10 ? "MEDIUM" : "LOW";
        var visibilityRisk = snapshot.VisibilityKm <= 1.5m ? "CRITICAL" : snapshot.VisibilityKm <= 5m ? "HIGH" : snapshot.VisibilityKm <= 10m ? "MEDIUM" : "LOW";

        var final = HigherRisk(HigherRisk(windRisk, rainRisk), visibilityRisk);
        var dominant = final == windRisk ? "WIND" : final == rainRisk ? "RAIN" : "VISIBILITY";
        var summary = $"Gió Beaufort {snapshot.BeaufortNumber}, mưa {snapshot.Rainfall1hMm:0.#} mm/h, tầm nhìn {(snapshot.VisibilityKm ?? 0):0.#} km.";
        return new SimulationRiskContext(windRisk, rainRisk, visibilityRisk, final, dominant, summary);
    }

    private static string HigherRisk(string left, string right)
    {
        static int Score(string value) => value switch
        {
            "CRITICAL" => 4,
            "HIGH" => 3,
            "MEDIUM" => 2,
            _ => 1
        };

        return Score(right) > Score(left) ? right : left;
    }

    private static async Task InsertDatasetWeatherReadingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid weatherReadingId,
        Guid portId,
        Guid? zoneId,
        Guid sessionId,
        SimulationSnapshotContext snapshot,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.weather_readings (
                id, port_id, zone_id, simulation_session_id, wind_speed_ms, beaufort_number,
                rainfall_1h_mm, visibility_km, weather_description, observed_at,
                data_source, source_record_key, raw_payload, is_simulation
            )
            VALUES (
                @id, @portId, @zoneId, @sessionId, @windSpeedMs, @beaufortNumber,
                @rainfall1hMm, @visibilityKm, 'Manual simulation snapshot', @observedAt,
                'SIMULATION_MANUAL', @sourceRecordKey, @rawPayload, TRUE
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", weatherReadingId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("zoneId", zoneId.HasValue ? zoneId.Value : DBNull.Value);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("windSpeedMs", snapshot.WindSpeedMs);
        command.Parameters.AddWithValue("beaufortNumber", snapshot.BeaufortNumber);
        command.Parameters.AddWithValue("rainfall1hMm", snapshot.Rainfall1hMm);
        command.Parameters.AddWithValue("visibilityKm", snapshot.VisibilityKm.HasValue ? snapshot.VisibilityKm.Value : DBNull.Value);
        command.Parameters.AddWithValue("observedAt", observedAt);
        command.Parameters.AddWithValue("sourceRecordKey", $"{sessionId}:manual-step-{snapshot.SnapshotNumber}");
        command.Parameters.Add("rawPayload", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(new { snapshot.SnapshotNumber, zoneId });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDatasetRiskAssessmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid riskAssessmentId,
        Guid weatherReadingId,
        Guid portId,
        Guid? zoneId,
        Guid sessionId,
        SimulationRiskContext risk,
        string previousRiskLevel,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.risk_assessments (
                id, weather_reading_id, port_id, zone_id, simulation_session_id,
                wind_risk_level, rain_risk_level, visibility_risk_level, final_risk_level,
                previous_risk_level, level_changed, dominant_factor, assessment_summary,
                threshold_version, evaluated_at, is_simulation
            )
            VALUES (
                @id, @weatherReadingId, @portId, @zoneId, @sessionId,
                @windRiskLevel::operational.risk_level_enum,
                @rainRiskLevel::operational.risk_level_enum,
                @visibilityRiskLevel::operational.risk_level_enum,
                @finalRiskLevel::operational.risk_level_enum,
                @previousRiskLevel::operational.risk_level_enum,
                @levelChanged,
                @dominantFactor::operational.weather_factor_enum,
                @assessmentSummary,
                1, @evaluatedAt, TRUE
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", riskAssessmentId);
        command.Parameters.AddWithValue("weatherReadingId", weatherReadingId);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("zoneId", zoneId.HasValue ? zoneId.Value : DBNull.Value);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("windRiskLevel", risk.WindRiskLevel);
        command.Parameters.AddWithValue("rainRiskLevel", risk.RainRiskLevel);
        command.Parameters.AddWithValue("visibilityRiskLevel", risk.VisibilityRiskLevel);
        command.Parameters.AddWithValue("finalRiskLevel", risk.FinalRiskLevel);
        command.Parameters.AddWithValue("previousRiskLevel", previousRiskLevel);
        command.Parameters.AddWithValue("levelChanged", previousRiskLevel != risk.FinalRiskLevel);
        command.Parameters.AddWithValue("dominantFactor", risk.DominantFactor);
        command.Parameters.AddWithValue("assessmentSummary", risk.Summary);
        command.Parameters.AddWithValue("evaluatedAt", observedAt.AddMinutes(1));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> InsertSimulationTasksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid portId,
        ZoneContext? zone,
        Guid sessionId,
        Guid riskAssessmentId,
        string riskLevel,
        CancellationToken cancellationToken)
    {
        var taskCode = $"SIM-{DateTimeOffset.UtcNow:HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        const string sql = """
            INSERT INTO operational.tasks (
                task_code, port_id, zone_id, title, description, priority, status,
                due_at, simulation_session_id
            )
            VALUES (
                @taskCode, @portId, @zoneId, @title, @description,
                @priority::operational.alert_severity_enum, 'NEW',
                NOW() + INTERVAL '2 hours', @sessionId
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskCode", taskCode);
        command.Parameters.AddWithValue("portId", portId);
        command.Parameters.AddWithValue("zoneId", zone is null ? DBNull.Value : zone.ZoneId);
        command.Parameters.AddWithValue("title", riskLevel == "CRITICAL" ? "Dừng hoạt động và kiểm tra an toàn" : "Kiểm tra khu vực rủi ro cao");
        command.Parameters.AddWithValue("description", $"Task sinh từ mô phỏng cho {zone?.ZoneName ?? "toàn cảng"} theo đánh giá {riskLevel}.");
        command.Parameters.AddWithValue("priority", riskLevel);
        command.Parameters.AddWithValue("sessionId", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return 1;
    }

    private static async Task UpdateGeneratedTaskCountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid sessionId,
        int generatedTaskCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE operational.simulation_sessions
            SET generated_task_count = @generatedTaskCount
            WHERE id = @sessionId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("sessionId", sessionId);
        command.Parameters.AddWithValue("generatedTaskCount", generatedTaskCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
    private sealed record DatasetContext(Guid DatasetId, string Name, string PortCode);
    private sealed record ZoneContext(Guid ZoneId, string ZoneName, string ZoneType, decimal? Latitude, decimal? Longitude);
    private sealed record SimulationSnapshotContext(
        int SnapshotNumber,
        decimal WindSpeedMs,
        short BeaufortNumber,
        decimal Rainfall1hMm,
        decimal? VisibilityKm,
        Guid? ZoneId);
    private sealed record SimulationRiskContext(
        string WindRiskLevel,
        string RainRiskLevel,
        string VisibilityRiskLevel,
        string FinalRiskLevel,
        string DominantFactor,
        string Summary);

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
    string FinalOperationMode)
{
    public int GeneratedTaskCount { get; init; }
}

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

public sealed record SimulationDatasetSummaryReadModel(
    Guid DatasetId,
    string Name,
    string? Description,
    string PortCode,
    int SnapshotCount);

public sealed record CreateSimulationDatasetReadModel(
    string Name,
    string? Description,
    string PortCode,
    IReadOnlyList<CreateSimulationSnapshotReadModel> Snapshots);

public sealed record CreateSimulationSnapshotReadModel(
    int SnapshotNumber,
    decimal WindSpeedMs,
    short BeaufortNumber,
    decimal Rainfall1hMm,
    decimal? VisibilityKm,
    Guid? ZoneId);

public sealed record SimulationResultReadModel(
    Guid SessionId,
    IReadOnlyList<SimulationMapPointReadModel> MapPoints,
    IReadOnlyList<SimulationDangerousZoneReadModel> DangerousZones,
    IReadOnlyList<SimulationGeneratedTaskReadModel> Tasks);

public sealed record SimulationMapPointReadModel(
    Guid ZoneId,
    string ZoneName,
    decimal Latitude,
    decimal Longitude,
    string RiskLevel);

public sealed record SimulationDangerousZoneReadModel(
    Guid ZoneId,
    string ZoneName,
    string RiskLevel,
    string? Reason);

public sealed record SimulationGeneratedTaskReadModel(
    string TaskCode,
    string Title,
    string Priority,
    string? ZoneName);
