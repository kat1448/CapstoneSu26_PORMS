using Npgsql;
using PORMS.Infrastructure.Data;
using System.Data;

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
