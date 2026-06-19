using Npgsql;
using PORMS.Infrastructure.Data;

namespace PORMS.Infrastructure.Repositories;

public sealed class RiskRepository
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public RiskRepository(NpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RiskTrendPointReadModel>> GetTrendAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TO_CHAR(evaluated_at AT TIME ZONE 'Asia/Ho_Chi_Minh', 'HH24:MI') AS hour_label,
                   final_risk_level
            FROM operational.risk_assessments
            WHERE zone_id IS NULL
            ORDER BY evaluated_at DESC
            LIMIT 8;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<RiskTrendPointReadModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var riskLevel = reader.GetString(1);
            results.Add(new RiskTrendPointReadModel(reader.GetString(0), RiskScore(riskLevel)));
        }

        if (results.Count == 0)
        {
            return
            [
                new("00:00", 1),
                new("06:00", 1),
                new("12:00", 1),
                new("18:00", 1)
            ];
        }

        results.Reverse();
        return results;
    }

    private static short RiskScore(string riskLevel)
    {
        return riskLevel switch
        {
            "LOW" => 1,
            "MEDIUM" => 2,
            "HIGH" => 3,
            "CRITICAL" => 4,
            _ => 1
        };
    }
}

public sealed record RiskTrendPointReadModel(string HourLabel, short RiskScore);
