using Npgsql;
using NpgsqlTypes;
using PORMS.Infrastructure.Data;
using System.Data;
using System.Text.Json;

namespace PORMS.Infrastructure.Repositories;

public sealed class SopRuleRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public SopRuleRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SopRulesReadModel> GetRulesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        var rules = new List<SopRuleReadModel>();
        const string rulesSql = """
            SELECT sr.id,
                   sr.rule_code,
                   sr.rule_name,
                   sr.description,
                   sr.trigger_risk_level::text,
                   sr.previous_risk_level::text,
                   sr.applies_to_zone_type::text,
                   sr.action_type::text,
                   sr.action_config::text,
                   sr.execution_order,
                   sr.is_active,
                   sr.version,
                   COALESCE(COUNT(se.id), 0) AS execution_count,
                   sr.updated_at
            FROM operational.sop_rules sr
            LEFT JOIN operational.sop_executions se ON se.sop_rule_id = sr.id
            WHERE sr.deleted_at IS NULL
            GROUP BY sr.id
            ORDER BY sr.execution_order, sr.rule_code;
            """;

        await using (var command = new NpgsqlCommand(rulesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rules.Add(new SopRuleReadModel(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetInt16(9),
                    reader.GetBoolean(10),
                    reader.GetInt32(11),
                    reader.GetInt64(12),
                    reader.GetFieldValue<DateTimeOffset>(13)));
            }
        }

        var executions = new List<SopExecutionReadModel>();
        const string executionsSql = """
            SELECT se.id,
                   sr.rule_code,
                   sr.rule_name,
                   ra.final_risk_level::text,
                   z.name,
                   sr.action_type::text,
                   se.status::text,
                   se.completed_at
            FROM operational.sop_executions se
            JOIN operational.sop_rules sr ON sr.id = se.sop_rule_id
            JOIN operational.risk_assessments ra ON ra.id = se.risk_assessment_id
            LEFT JOIN operational.zones z ON z.id = se.zone_id
            ORDER BY se.created_at DESC
            LIMIT 10;
            """;

        await using (var command = new NpgsqlCommand(executionsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                executions.Add(new SopExecutionReadModel(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)));
            }
        }

        const string summarySql = """
            SELECT (SELECT COUNT(*) FROM operational.sop_rules WHERE deleted_at IS NULL) AS total_rules,
                   (SELECT COUNT(*) FROM operational.sop_rules WHERE deleted_at IS NULL AND is_active = TRUE) AS active_rules,
                   (SELECT COUNT(*) FROM operational.sop_executions WHERE created_at >= NOW() - INTERVAL '30 days') AS recent_executions,
                   (SELECT COUNT(*) FROM operational.sop_rules WHERE deleted_at IS NULL AND action_type = 'CREATE_TASK') AS automated_tasks;
            """;

        SopRulesSummaryReadModel summary;
        await using (var command = new NpgsqlCommand(summarySql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            summary = new SopRulesSummaryReadModel(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3));
        }

        return new SopRulesReadModel(summary, rules, executions);
    }

    /// Tải toàn bộ SOP chưa bị xóa để tạo template và so sánh khi import
    /// Bao gồm cả rule đang tắt để người dùng có thể bật lại bằng Excel
    public async Task<IReadOnlyList<SopRuleImportReadModel>> GetImportRulesAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id,
                   rule_code,
                   rule_name,
                   description,
                   trigger_risk_level::text,
                   previous_risk_level::text,
                   applies_to_zone_type::text,
                   action_type::text,
                   action_config::text,
                   execution_order,
                   is_active,
                   version
            FROM operational.sop_rules
            WHERE deleted_at IS NULL
            ORDER BY
                CASE trigger_risk_level::text
                    WHEN 'LOW' THEN 1
                    WHEN 'MEDIUM' THEN 2
                    WHEN 'HIGH' THEN 3
                    WHEN 'CRITICAL' THEN 4
                    ELSE 99
                END,
                execution_order,
                rule_code;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var rules = new List<SopRuleImportReadModel>();

        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(new SopRuleImportReadModel(
                Id: reader.GetGuid(0),
                RuleCode: reader.GetString(1),
                RuleName: reader.GetString(2),
                Description: reader.IsDBNull(3)
                    ? null
                    : reader.GetString(3),
                TriggerRiskLevel: reader.GetString(4),
                PreviousRiskLevel: reader.IsDBNull(5)
                    ? null
                    : reader.GetString(5),
                AppliesToZoneType: reader.IsDBNull(6)
                    ? null
                    : reader.GetString(6),
                ActionType: reader.GetString(7),
                ActionConfigJson: reader.GetString(8),
                ExecutionOrder: reader.GetInt16(9),
                IsActive: reader.GetBoolean(10),
                Version: reader.GetInt32(11)));
        }

        return rules;
    }

    /// Tạo hoặc cập nhật nhiều SOP trong cùng một transaction
    /// Nếu một dòng thất bại, toàn bộ import sẽ được rollback
    public async Task<Guid> ImportRulesAsync(
        IReadOnlyList<SaveSopRuleImportReadModel> rules,
        SopRuleImportAuditReadModel audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(audit);

        if (audit.ActorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Không xác định được người thực hiện import.",
                nameof(audit));
        }

        if (string.IsNullOrWhiteSpace(audit.FileName))
        {
            throw new ArgumentException(
                "Tên file import là bắt buộc.",
                nameof(audit));
        }

        if (string.IsNullOrWhiteSpace(audit.ChangeReason))
        {
            throw new ArgumentException(
                "Lý do import là bắt buộc.",
                nameof(audit));
        }

        if (rules.Any(rule =>
                string.IsNullOrWhiteSpace(rule.RuleCode)))
        {
            throw new ArgumentException(
                "Danh sách import chứa RuleCode trống.",
                nameof(rules));
        }

        var duplicateRuleCode = rules
            .GroupBy(
                rule => rule.RuleCode.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateRuleCode is not null)
        {
            throw new ArgumentException(
                $"RuleCode {duplicateRuleCode.Key} xuất hiện nhiều lần.",
                nameof(rules));
        }

        var importBatchId = Guid.NewGuid();

        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Import là thao tác hiếm nhưng thay đổi nhiều rule.
        // Khóa ngắn giúp tránh CRUD thủ công chạy xen giữa quá trình import.
        const string lockSql = """
            LOCK TABLE operational.sop_rules
            IN SHARE ROW EXCLUSIVE MODE;
            """;

        await using (var lockCommand =
                     new NpgsqlCommand(
                         lockSql,
                         connection,
                         transaction))
        {
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedAction =
                rule.ExpectedAction.Trim().ToUpperInvariant();

            if (expectedAction is not ("CREATE" or "UPDATE"))
            {
                throw new ArgumentException(
                    $"ExpectedAction của {rule.RuleCode} phải là CREATE hoặc UPDATE.",
                    nameof(rules));
            }

            var existingRuleId =
                await FindActiveRuleIdForUpdateAsync(
                    connection,
                    transaction,
                    rule.RuleCode,
                    cancellationToken);

            if (expectedAction == "CREATE")
            {
                // Database đã thay đổi sau preview, không tự đổi CREATE thành UPDATE
                if (existingRuleId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"SOP {rule.RuleCode} đã được tạo sau bước preview. " +
                        "Vui lòng preview lại file.");
                }

                await InsertImportedRuleAsync(
                    connection,
                    transaction,
                    rule,
                    audit,
                    cancellationToken);

                createdCount++;
                continue;
            }

            // Database đã thay đổi sau preview, không tạo lại rule bị xóa
            if (!existingRuleId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Không còn tìm thấy SOP {rule.RuleCode}. " +
                    "Vui lòng preview lại file.");
            }

            await UpdateImportedRuleAsync(
                connection,
                transaction,
                existingRuleId.Value,
                rule,
                audit,
                cancellationToken);

            updatedCount++;
        }

        // Xác nhận dữ liệu ghi thực tế vẫn khớp với preview
        if (createdCount != audit.CreatedCount ||
            updatedCount != audit.UpdatedCount)
        {
            throw new InvalidOperationException(
                "Kết quả import không còn khớp với preview. " +
                "Vui lòng kiểm tra lại file.");
        }

        await InsertImportAuditEventAsync(
            connection,
            transaction,
            importBatchId,
            audit,
            rules.Count,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return importBatchId;
    }

    public async Task<SopRuleReadModel> CreateRuleAsync(SaveSopRuleReadModel input, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO operational.sop_rules (
                rule_code,
                rule_name,
                description,
                trigger_risk_level,
                previous_risk_level,
                applies_to_zone_type,
                action_type,
                action_config,
                execution_order,
                is_active,
                change_reason
            )
            VALUES (
                @ruleCode,
                @ruleName,
                @description,
                @triggerRiskLevel::operational.risk_level_enum,
                @previousRiskLevel::operational.risk_level_enum,
                @appliesToZoneType::operational.zone_type_enum,
                @actionType::operational.sop_action_type_enum,
                @actionConfig::jsonb,
                @executionOrder,
                @isActive,
                @changeReason
            )
            RETURNING id, rule_code, rule_name, description, trigger_risk_level::text,
                      previous_risk_level::text, applies_to_zone_type::text, action_type::text,
                      action_config::text, execution_order, is_active, version, 0::bigint, updated_at;
            """;

        return await ExecuteRuleCommandAsync(connection, null, sql, input, cancellationToken);
    }

    public async Task<SopRuleReadModel?> UpdateRuleAsync(Guid ruleId, SaveSopRuleReadModel input, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            UPDATE operational.sop_rules
            SET rule_code = @ruleCode,
                rule_name = @ruleName,
                description = @description,
                trigger_risk_level = @triggerRiskLevel::operational.risk_level_enum,
                previous_risk_level = @previousRiskLevel::operational.risk_level_enum,
                applies_to_zone_type = @appliesToZoneType::operational.zone_type_enum,
                action_type = @actionType::operational.sop_action_type_enum,
                action_config = @actionConfig::jsonb,
                execution_order = @executionOrder,
                is_active = @isActive,
                change_reason = @changeReason,
                version = version + 1,
                updated_at = NOW()
            WHERE id = @ruleId
              AND deleted_at IS NULL
            RETURNING id, rule_code, rule_name, description, trigger_risk_level::text,
                      previous_risk_level::text, applies_to_zone_type::text, action_type::text,
                      action_config::text, execution_order, is_active, version,
                      (SELECT COUNT(*) FROM operational.sop_executions se WHERE se.sop_rule_id = operational.sop_rules.id)::bigint,
                      updated_at;
            """;

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = BuildSaveCommand(connection, transaction, sql, input);
        command.Parameters.AddWithValue("ruleId", ruleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var rule = ReadRule(reader);
        await reader.DisposeAsync();
        await transaction.CommitAsync(cancellationToken);
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
        const string sql = """
            UPDATE operational.sop_rules
            SET deleted_at = NOW(),
                is_active = FALSE,
                updated_at = NOW()
            WHERE id = @ruleId
              AND deleted_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ruleId", ruleId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    /// Khóa rule hiện tại theo RuleCode để bảo vệ bước UPDATE
    private static async Task<Guid?> FindActiveRuleIdForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string ruleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id
            FROM operational.sop_rules
            WHERE UPPER(rule_code) = @ruleCode
              AND deleted_at IS NULL
            FOR UPDATE;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue(
            "ruleCode",
            ruleCode.Trim().ToUpperInvariant());

        var result =
            await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid ruleId
            ? ruleId
            : null;
    }

    private static async Task InsertImportedRuleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SaveSopRuleImportReadModel rule,
        SopRuleImportAuditReadModel audit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.sop_rules (
                rule_code,
                rule_name,
                description,
                trigger_risk_level,
                previous_risk_level,
                applies_to_zone_type,
                action_type,
                action_config,
                execution_order,
                is_active,
                version,
                change_reason,
                created_by_user_id,
                updated_by_user_id,
                created_at,
                updated_at
            )
            VALUES (
                @ruleCode,
                @ruleName,
                @description,
                @triggerRiskLevel::operational.risk_level_enum,
                @previousRiskLevel::operational.risk_level_enum,
                @appliesToZoneType::operational.zone_type_enum,
                @actionType::operational.sop_action_type_enum,
                @actionConfig::jsonb,
                @executionOrder,
                @isActive,
                1,
                @changeReason,
                @actorUserId,
                @actorUserId,
                NOW(),
                NOW()
            );
            """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        AddImportRuleParameters(command, rule, audit);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateImportedRuleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid ruleId,
        SaveSopRuleImportReadModel rule,
        SopRuleImportAuditReadModel audit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE operational.sop_rules
            SET rule_code = @ruleCode,
                rule_name = @ruleName,
                description = @description,
                trigger_risk_level =
                    @triggerRiskLevel::operational.risk_level_enum,
                previous_risk_level =
                    @previousRiskLevel::operational.risk_level_enum,
                applies_to_zone_type =
                    @appliesToZoneType::operational.zone_type_enum,
                action_type =
                    @actionType::operational.sop_action_type_enum,
                action_config = @actionConfig::jsonb,
                execution_order = @executionOrder,
                is_active = @isActive,
                version = version + 1,
                change_reason = @changeReason,
                updated_by_user_id = @actorUserId,
                updated_at = NOW()
            WHERE id = @ruleId
              AND deleted_at IS NULL;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("ruleId", ruleId);

        AddImportRuleParameters(command, rule, audit);

        var affectedRows =
            await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Không thể cập nhật SOP {rule.RuleCode}.");
        }
    }

    /// Gán parameter dùng chung cho câu lệnh INSERT và UPDATE
    private static void AddImportRuleParameters(
        NpgsqlCommand command,
        SaveSopRuleImportReadModel rule,
        SopRuleImportAuditReadModel audit)
    {
        command.Parameters.AddWithValue(
            "ruleCode",
            rule.RuleCode.Trim().ToUpperInvariant());

        command.Parameters.AddWithValue(
            "ruleName",
            rule.RuleName.Trim());

        command.Parameters
            .Add("description", NpgsqlDbType.Text)
            .Value = string.IsNullOrWhiteSpace(rule.Description)
                ? DBNull.Value
                : rule.Description.Trim();

        command.Parameters.AddWithValue(
            "triggerRiskLevel",
            rule.TriggerRiskLevel.Trim().ToUpperInvariant());

        command.Parameters
            .Add("previousRiskLevel", NpgsqlDbType.Text)
            .Value = string.IsNullOrWhiteSpace(rule.PreviousRiskLevel)
                ? DBNull.Value
                : rule.PreviousRiskLevel.Trim().ToUpperInvariant();

        command.Parameters
            .Add("appliesToZoneType", NpgsqlDbType.Text)
            .Value = string.IsNullOrWhiteSpace(rule.AppliesToZoneType)
                ? DBNull.Value
                : rule.AppliesToZoneType.Trim().ToUpperInvariant();

        command.Parameters.AddWithValue(
            "actionType",
            rule.ActionType.Trim().ToUpperInvariant());

        command.Parameters
            .Add("actionConfig", NpgsqlDbType.Jsonb)
            .Value = rule.ActionConfigJson;

        command.Parameters.AddWithValue(
            "executionOrder",
            rule.ExecutionOrder);

        command.Parameters.AddWithValue(
            "isActive",
            rule.IsActive);

        command.Parameters
            .Add("changeReason", NpgsqlDbType.Text)
            .Value = audit.ChangeReason.Trim();

        command.Parameters.AddWithValue(
            "actorUserId",
            audit.ActorUserId);
    }

    /// Ghi một operation event đại diện cho toàn bộ batch import
    private static async Task InsertImportAuditEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid importBatchId,
        SopRuleImportAuditReadModel audit,
        int changedRowCount,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO operational.operation_events (
                id,
                event_type,
                actor_user_id,
                entity_type,
                entity_id,
                summary,
                payload,
                correlation_id,
                occurred_at
            )
            VALUES (
                @id,
                'SOP_RULES_IMPORTED',
                @actorUserId,
                'sop_rule_import',
                @entityId,
                @summary,
                @payload,
                @correlationId,
                NOW()
            );
            """;

        var safeFileName = Path.GetFileName(audit.FileName);

        var payload = JsonSerializer.Serialize(new
        {
            importBatchId,
            fileName = safeFileName,
            changeReason = audit.ChangeReason.Trim(),
            audit.CreatedCount,
            audit.UpdatedCount,
            audit.UnchangedCount,
            changedRowCount
        });

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue(
            "actorUserId",
            audit.ActorUserId);

        command.Parameters.AddWithValue(
            "entityId",
            importBatchId);

        command.Parameters.AddWithValue(
            "summary",
            $"Nhập quy tắc SOP từ file {safeFileName}.");

        command.Parameters
            .Add("payload", NpgsqlDbType.Jsonb)
            .Value = payload;

        command.Parameters.AddWithValue(
            "correlationId",
            importBatchId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SopRuleReadModel> ExecuteRuleCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        SaveSopRuleReadModel input,
        CancellationToken cancellationToken)
    {
        await using var command = BuildSaveCommand(connection, transaction, sql, input);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRule(reader);
    }

    private static NpgsqlCommand BuildSaveCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        SaveSopRuleReadModel input)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("ruleCode", input.RuleCode.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("ruleName", input.RuleName.Trim());
        command.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(input.Description) ? DBNull.Value : input.Description.Trim());
        command.Parameters.AddWithValue("triggerRiskLevel", input.TriggerRiskLevel.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("previousRiskLevel", string.IsNullOrWhiteSpace(input.PreviousRiskLevel) ? DBNull.Value : input.PreviousRiskLevel.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("appliesToZoneType", string.IsNullOrWhiteSpace(input.AppliesToZoneType) ? DBNull.Value : input.AppliesToZoneType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("actionType", input.ActionType.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("actionConfig", input.ActionConfigText);
        command.Parameters.AddWithValue("executionOrder", input.ExecutionOrder);
        command.Parameters.AddWithValue("isActive", input.IsActive);
        command.Parameters.AddWithValue("changeReason", string.IsNullOrWhiteSpace(input.ChangeReason) ? DBNull.Value : input.ChangeReason.Trim());
        return command;
    }

    private static SopRuleReadModel ReadRule(NpgsqlDataReader reader)
    {
        return new SopRuleReadModel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetInt16(9),
            reader.GetBoolean(10),
            reader.GetInt32(11),
            reader.GetInt64(12),
            reader.GetFieldValue<DateTimeOffset>(13));
    }
}

public sealed record SopRulesReadModel(
    SopRulesSummaryReadModel Summary,
    IReadOnlyList<SopRuleReadModel> Rules,
    IReadOnlyList<SopExecutionReadModel> Executions);
public sealed record SopRulesSummaryReadModel(
    long TotalRules,
    long ActiveRules,
    long RecentExecutions,
    long AutomatedTasks);
public sealed record SopRuleReadModel(
    Guid Id,
    string RuleCode,
    string RuleName,
    string? Description,
    string TriggerRiskLevel,
    string? PreviousRiskLevel,
    string? AppliesToZoneType,
    string ActionType,
    string ActionConfigText,
    short ExecutionOrder,
    bool IsActive,
    int Version,
    long ExecutionCount,
    DateTimeOffset UpdatedAt);
public sealed record SopExecutionReadModel(
    Guid Id,
    string RuleCode,
    string RuleName,
    string RiskLevel,
    string? ZoneName,
    string ActionType,
    string Status,
    DateTimeOffset? CompletedAt);

/// Dữ liệu SOP tối thiểu dùng cho template và so sánh import
public sealed record SopRuleImportReadModel(
    Guid Id,
    string RuleCode,
    string RuleName,
    string? Description,
    string TriggerRiskLevel,
    string? PreviousRiskLevel,
    string? AppliesToZoneType,
    string ActionType,
    string ActionConfigJson,
    short ExecutionOrder,
    bool IsActive,
    int Version);

/// Dữ liệu đã được kiểm tra và sẵn sàng ghi database
public sealed record SaveSopRuleImportReadModel(
    string ExpectedAction,
    string RuleCode,
    string RuleName,
    string? Description,
    string TriggerRiskLevel,
    string? PreviousRiskLevel,
    string? AppliesToZoneType,
    string ActionType,
    string ActionConfigJson,
    short ExecutionOrder,
    bool IsActive);

/// Thông tin truy vết của một lần import SOP
public sealed record SopRuleImportAuditReadModel(
    Guid ActorUserId,
    string FileName,
    string ChangeReason,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount);

public sealed record SaveSopRuleReadModel(
    string RuleCode,
    string RuleName,
    string? Description,
    string TriggerRiskLevel,
    string? PreviousRiskLevel,
    string? AppliesToZoneType,
    string ActionType,
    string ActionConfigText,
    short ExecutionOrder,
    bool IsActive,
    string? ChangeReason);
