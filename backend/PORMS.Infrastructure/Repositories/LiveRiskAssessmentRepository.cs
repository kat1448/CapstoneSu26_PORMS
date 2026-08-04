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

        /// Đánh giá một weather reading thật trong một transaction duy nhất
        /// Request lặp lại sẽ trả về assessment cũ thay vì tạo thêm dữ liệu
        public async Task<LiveRiskAssessmentReadModel?> EvaluateWeatherReadingAsync(
            Guid portId,
            Guid weatherReadingId,
            CancellationToken cancellationToken)
        {
            await using var connection =
                await _connectionFactory.OpenAsync(cancellationToken);

            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            // Khóa reading và xác nhận reading hợp lệ cho luồng dữ liệu thật
            var weatherReading = await GetWeatherReadingForUpdateAsync(
                connection,
                transaction,
                portId,
                weatherReadingId,
                cancellationToken);

            if (weatherReading is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            // Prefect có thể retry cùng request, nên trả lại kết quả đã có
            var existingAssessment = await GetExistingAssessmentAsync(
                connection,
                transaction,
                portId,
                weatherReadingId,
                cancellationToken);

            if (existingAssessment is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return existingAssessment;
            }

            var thresholds = await GetGlobalRiskThresholdsAsync(
                connection,
                transaction,
                cancellationToken);

            // Khóa port để các reading khác nhau của cùng cảng không cập nhật song song
            var previousRiskLevel =
                await GetCurrentPortRiskLevelForUpdateAsync(
                    connection,
                    transaction,
                    portId,
                    cancellationToken);

            var evaluation = EvaluateWeatherReading(
                weatherReading,
                thresholds);

            var assessment = await InsertRiskAssessmentAsync(
                connection,
                transaction,
                weatherReading,
                evaluation,
                previousRiskLevel,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return assessment;
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
        private static async Task<IReadOnlyList<RiskThresholdRule>> GetGlobalRiskThresholdsAsync(
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

        /// Khóa và tải mức rủi ro hiện tại của cảng
        /// Khóa port giúp các weather reading của cùng cảng được xử lý tuần tự
        private static async Task<string> GetCurrentPortRiskLevelForUpdateAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid portId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT current_risk_level::text
                FROM operational.ports
                WHERE id = @portId
                    AND deleted_at IS NULL
                    AND is_active = TRUE
                FOR UPDATE;
                """;

            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("portId", portId);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is not string currentRiskLevel)
            {
                throw new InvalidOperationException(
                    "Không thể tải mức rủi ro hiện tại của cảng.");
            }

            return currentRiskLevel;
        }

        /// Chuyển weather reading thành input chuẩn và gọi Risk Engine dùng chung
        /// Repository không tự chứa công thức threshold riêng
        private WeatherRiskEvaluation EvaluateWeatherReading(
            LiveWeatherReadingContext weatherReading,
            IReadOnlyList<RiskThresholdRule> thresholds)
        {
            return _riskThresholdEvaluator.Evaluate(
                new WeatherRiskInput(
                    weatherReading.BeaufortNumber,
                    weatherReading.Rainfall1hMm,
                    weatherReading.VisibilityKm),
                thresholds);
        }

        /// Lưu kết quả đánh giá thật và trả về dữ liệu cho API
        /// Trigger database sẽ đồng bộ current_risk_level của cảng sau khi insert
        private static async Task<LiveRiskAssessmentReadModel> InsertRiskAssessmentAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            LiveWeatherReadingContext weatherReading,
            WeatherRiskEvaluation evaluation,
            string previousRiskLevel,
            CancellationToken cancellationToken)
        {
            var riskAssessmentId = Guid.NewGuid();

            var levelChanged = !string.Equals(
                previousRiskLevel,
                evaluation.FinalRiskLevel,
                StringComparison.Ordinal);

            const string sql = """
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
                    threshold_version,
                    evaluated_at,
                    is_simulation
                )
                VALUES (
                    @id,
                    @weatherReadingId,
                    @portId,
                    @windRiskLevel::operational.risk_level_enum,
                    @rainRiskLevel::operational.risk_level_enum,
                    @visibilityRiskLevel::operational.risk_level_enum,
                    @finalRiskLevel::operational.risk_level_enum,
                    @previousRiskLevel::operational.risk_level_enum,
                    @levelChanged,
                    @dominantFactor::operational.weather_factor_enum,
                    @assessmentSummary,
                    1,
                    NOW(),
                    FALSE
                );
                """;

            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("id", riskAssessmentId);
            command.Parameters.AddWithValue(
                "weatherReadingId",
                weatherReading.WeatherReadingId);
            command.Parameters.AddWithValue("portId", weatherReading.PortId);
            command.Parameters.AddWithValue(
                "windRiskLevel",
                evaluation.Wind.RiskLevel);
            command.Parameters.AddWithValue(
                "rainRiskLevel",
                evaluation.Rain.RiskLevel);
            command.Parameters.AddWithValue(
                "visibilityRiskLevel",
                evaluation.Visibility.RiskLevel);
            command.Parameters.AddWithValue(
                "finalRiskLevel",
                evaluation.FinalRiskLevel);
            command.Parameters.AddWithValue(
                "previousRiskLevel",
                previousRiskLevel);
            command.Parameters.AddWithValue("levelChanged", levelChanged);
            command.Parameters.AddWithValue(
                "dominantFactor",
                evaluation.DominantFactor);
            command.Parameters.AddWithValue(
                "assessmentSummary",
                evaluation.Summary);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return new LiveRiskAssessmentReadModel(
                riskAssessmentId,
                weatherReading.PortId,
                weatherReading.WeatherReadingId,
                Created: true,
                evaluation.Wind.RiskLevel,
                evaluation.Rain.RiskLevel,
                evaluation.Visibility.RiskLevel,
                evaluation.FinalRiskLevel,
                previousRiskLevel,
                levelChanged,
                evaluation.DominantFactor,
                evaluation.Summary);
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
