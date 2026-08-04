using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using PORMS.Infrastructure.Data;
using PORMS.Infrastructure.Services;

namespace PORMS.Infrastructure.Repositories
{
    /// Điều phối việc đánh giá một weather reading thật trong một transaction
    /// Repository này không xử lý dữ liệu simulation
    public sealed class LiveRiskAssessmentRepository
    {
        private readonly NpgsqlConnectionFactory _connectionFactory;
        private readonly RiskThresholdEvaluator _riskThresholdEvaluator;

        public LiveRiskAssessmentRepository(
            NpgsqlConnectionFactory connectionFactory,
            RiskThresholdEvaluator riskThresholdEvaluator)
        {
            _connectionFactory = connectionFactory;
            _riskThresholdEvaluator = riskThresholdEvaluator;
        }

        /// Khóa weather reading để các request retry không xử lý đồng thời
        /// Đồng thời xác nhận reading thuộc đúng cảng và không phải simulation
        private static async Task<LiveWeatherReadingContext?> GetWeatherReadingForUpdateAsync(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction,
                Guid portId,
                Guid weatherReadingId,
                CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT w.id,
                        w.port_id,
                        w.beaufort_number,
                        w.rainfall_1h_mm,
                        w.visibility_km
                FROM operational.weather_readings w
                JOIN operational.ports p ON p.id = w.port_id
                WHERE w.id = @weatherReadingId
                    AND w.port_id = @portId
                    AND w.is_simulation = FALSE
                    AND p.deleted_at IS NULL
                    AND p.is_active = TRUE
                FOR UPDATE OF w;
                """;

            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue(
                "weatherReadingId",
                weatherReadingId);
            command.Parameters.AddWithValue("portId", portId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new LiveWeatherReadingContext(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetInt16(2),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4));
        }

        /// Trả lại assessment đã tồn tại để endpoint có tính idempotent
        /// Weather reading chỉ được phép có tối đa một assessment
        private static async Task<LiveRiskAssessmentReadModel?> GetExistingAssessmentAsync(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction,
                Guid portId,
                Guid weatherReadingId,
                CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT id,
                    wind_risk_level::text,
                    rain_risk_level::text,
                    visibility_risk_level::text,
                    final_risk_level::text,
                    previous_risk_level::text,
                    level_changed,
                    dominant_factor::text,
                    assessment_summary
                FROM operational.risk_assessments
                WHERE weather_reading_id = @weatherReadingId
                AND port_id = @portId
                AND is_simulation = FALSE
                LIMIT 1;
                """;

            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue(
                "weatherReadingId",
                weatherReadingId);
            command.Parameters.AddWithValue("portId", portId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new LiveRiskAssessmentReadModel(
                reader.GetGuid(0),
                portId,
                weatherReadingId,
                Created: false,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? string.Empty : reader.GetString(8));
        }

        /// Tải threshold Version 1 bằng chính connection và transaction hiện tại
        /// Điều này tránh việc cấu hình thay đổi giữa lúc đọc weather và ghi assessment
        private static async Task<IReadOnlyList<RiskThresholdRule>>
            GetGlobalRiskThresholdsAsync(
                NpgsqlConnection connection,
                NpgsqlTransaction transaction,
                CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT factor::text,
                    risk_level::text,
                    comparison_operator::text,
                    threshold_value,
                    is_enabled
                FROM operational.risk_thresholds
                WHERE version = 1
                ORDER BY factor, risk_level;
                """;

            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var thresholds = new List<RiskThresholdRule>();

            while (await reader.ReadAsync(cancellationToken))
            {
                thresholds.Add(new RiskThresholdRule(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetDecimal(3),
                    reader.GetBoolean(4)));
            }

            return thresholds;
        }
    }

    /// Dữ liệu thời tiết tối thiểu cần cho Risk Engine
    internal sealed record LiveWeatherReadingContext(
        Guid WeatherReadingId,
        Guid PortId,
        short BeaufortNumber,
        decimal Rainfall1hMm,
        decimal? VisibilityKm);

    /// Kết quả được trả về cho API nội bộ và manual refresh
    public sealed record LiveRiskAssessmentReadModel(
        Guid RiskAssessmentId,
        Guid PortId,
        Guid WeatherReadingId,
        bool Created,
        string WindRiskLevel,
        string RainRiskLevel,
        string VisibilityRiskLevel,
        string FinalRiskLevel,
        string? PreviousRiskLevel,
        bool LevelChanged,
        string DominantFactor,
        string Summary);
}
