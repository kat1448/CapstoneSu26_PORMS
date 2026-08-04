using System;
using System.Collections.Generic;
using System.Text;

namespace PORMS.Infrastructure.Services
{
    /// Đánh giá rủi ro thời tiết bằng cấu hình threshold được cung cấp
    /// Class này không truy cập database và không tạo side effect
    public sealed class RiskThresholdEvaluator
    {
        private static readonly string[] SupportedFactors =
        [
            "WIND",
        "RAIN",
        "VISIBILITY"
        ];

        private static readonly string[] SupportedRiskLevels =
        [
            "LOW",
        "MEDIUM",
        "HIGH",
        "CRITICAL"
        ];

        /// Đánh giá từng yếu tố và chọn mức rủi ro cao nhất
        /// Các threshold phải là cấu hình Version 1 đã được hợp nhất
        public WeatherRiskEvaluation Evaluate(
            WeatherRiskInput input,
            IReadOnlyList<RiskThresholdRule> thresholds)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(thresholds);

            ValidateWeatherInput(input);

            var normalizedThresholds = thresholds
                .Select(NormalizeRule)
                .ToList();

            ValidateThresholdConfiguration(normalizedThresholds);

            var wind = EvaluateFactor(
                "WIND",
                input.BeaufortNumber,
                hasMeasurement: true,
                normalizedThresholds);

            var rain = EvaluateFactor(
                "RAIN",
                input.Rainfall1hMm,
                hasMeasurement: true,
                normalizedThresholds);

            var visibility = EvaluateFactor(
                "VISIBILITY",
                input.VisibilityKm,
                hasMeasurement: input.VisibilityKm.HasValue,
                normalizedThresholds);

            // Thứ tự này đồng thời là quy tắc xử lý khi nhiều yếu tố cùng mức rủi ro
            var dominant = new[] { wind, rain, visibility }
                .OrderByDescending(result => RiskScore(result.RiskLevel))
                .First();

            var visibilityText = input.VisibilityKm.HasValue
                ? $"{input.VisibilityKm.Value:0.###} km"
                : "không có dữ liệu";

            var summary =
                $"Gió Beaufort {input.BeaufortNumber}, " +
                $"mưa {input.Rainfall1hMm:0.###} mm/h, " +
                $"tầm nhìn {visibilityText}.";

            return new WeatherRiskEvaluation(
                wind,
                rain,
                visibility,
                dominant.RiskLevel,
                dominant.Factor,
                summary);
        }

        private static FactorRiskEvaluation EvaluateFactor(
            string factor,
            decimal? measuredValue,
            bool hasMeasurement,
            IReadOnlyList<RiskThresholdRule> thresholds)
        {
            // Nếu nguồn thời tiết không cung cấp giá trị thì không tự suy đoán rủi ro
            if (!hasMeasurement || !measuredValue.HasValue)
            {
                return new FactorRiskEvaluation(
                    factor,
                    "LOW",
                    null,
                    false);
            }

            var matchedRule = thresholds
                .Where(rule =>
                    rule.Factor == factor &&
                    rule.IsEnabled &&
                    IsMatched(measuredValue.Value, rule))
                .OrderByDescending(rule => RiskScore(rule.RiskLevel))
                .FirstOrDefault();

            return new FactorRiskEvaluation(
                factor,
                matchedRule?.RiskLevel ?? "LOW",
                measuredValue,
                true);
        }

        private static bool IsMatched(
            decimal measuredValue,
            RiskThresholdRule rule)
        {
            return rule.ComparisonOperator switch
            {
                "GTE" => measuredValue >= rule.ThresholdValue,
                "LTE" => measuredValue <= rule.ThresholdValue,
                _ => throw new InvalidOperationException(
                    $"Toán tử threshold {rule.ComparisonOperator} không được hỗ trợ.")
            };
        }

        private static RiskThresholdRule NormalizeRule(
            RiskThresholdRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            return new RiskThresholdRule(
                NormalizeRequired(rule.Factor, nameof(rule.Factor)),
                NormalizeRequired(rule.RiskLevel, nameof(rule.RiskLevel)),
                NormalizeOperator(rule.ComparisonOperator),
                rule.ThresholdValue,
                rule.IsEnabled);
        }

        private static void ValidateWeatherInput(WeatherRiskInput input)
        {
            if (input.BeaufortNumber is < 0 or > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "Beaufort phải nằm trong khoảng từ 0 đến 12.");
            }

            if (input.Rainfall1hMm < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "Lượng mưa không được là số âm.");
            }

            if (input.VisibilityKm is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "Tầm nhìn không được là số âm.");
            }
        }

        private static void ValidateThresholdConfiguration(
            IReadOnlyList<RiskThresholdRule> thresholds)
        {
            if (thresholds.Count == 0)
            {
                throw new InvalidOperationException(
                    "Không có cấu hình threshold để đánh giá rủi ro.");
            }

            foreach (var threshold in thresholds)
            {
                if (!SupportedFactors.Contains(threshold.Factor))
                {
                    throw new InvalidOperationException(
                        $"Yếu tố {threshold.Factor} không được hỗ trợ.");
                }

                if (!SupportedRiskLevels.Contains(threshold.RiskLevel))
                {
                    throw new InvalidOperationException(
                        $"Mức rủi ro {threshold.RiskLevel} không được hỗ trợ.");
                }

                if (threshold.ThresholdValue < 0)
                {
                    throw new InvalidOperationException(
                        "Giá trị threshold không được là số âm.");
                }

                ValidateOperatorDirection(threshold);
            }

            // Mỗi yếu tố phải có đúng một rule cho mỗi mức rủi ro
            foreach (var factor in SupportedFactors)
            {
                foreach (var riskLevel in SupportedRiskLevels)
                {
                    var count = thresholds.Count(rule =>
                        rule.Factor == factor &&
                        rule.RiskLevel == riskLevel);

                    if (count != 1)
                    {
                        throw new InvalidOperationException(
                            $"Cấu hình phải có đúng một threshold cho {factor}/{riskLevel}.");
                    }
                }

                if (!thresholds.Any(rule =>
                        rule.Factor == factor &&
                        rule.IsEnabled))
                {
                    throw new InvalidOperationException(
                        $"Yếu tố {factor} phải có ít nhất một threshold đang bật.");
                }
            }
        }

        private static void ValidateOperatorDirection(
            RiskThresholdRule threshold)
        {
            if (threshold.Factor is "WIND" or "RAIN")
            {
                if (threshold.ComparisonOperator != "GTE")
                {
                    throw new InvalidOperationException(
                        $"{threshold.Factor} chỉ hỗ trợ toán tử GTE.");
                }

                return;
            }

            var expectedOperator = threshold.RiskLevel == "LOW"
                ? "GTE"
                : "LTE";

            if (threshold.ComparisonOperator != expectedOperator)
            {
                throw new InvalidOperationException(
                    $"VISIBILITY/{threshold.RiskLevel} phải sử dụng {expectedOperator}.");
            }
        }

        private static int RiskScore(string riskLevel)
        {
            return riskLevel switch
            {
                "LOW" => 1,
                "MEDIUM" => 2,
                "HIGH" => 3,
                "CRITICAL" => 4,
                _ => throw new InvalidOperationException(
                    $"Mức rủi ro {riskLevel} không được hỗ trợ.")
            };
        }

        private static string NormalizeRequired(
            string value,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{fieldName} không được để trống.");
            }

            return value.Trim().ToUpperInvariant();
        }

        private static string NormalizeOperator(string value)
        {
            var normalized = NormalizeRequired(
                value,
                nameof(RiskThresholdRule.ComparisonOperator));

            return normalized switch
            {
                "GTE" or ">=" => "GTE",
                "LTE" or "<=" => "LTE",
                _ => throw new InvalidOperationException(
                    $"Toán tử threshold {value} không được hỗ trợ.")
            };
        }
    }

    /// Một rule tối thiểu cần thiết để Risk Engine đánh giá
    public sealed record RiskThresholdRule(
        string Factor,
        string RiskLevel,
        string ComparisonOperator,
        decimal ThresholdValue,
        bool IsEnabled);

    /// Dữ liệu thời tiết đã được chuẩn hóa về đơn vị hệ thống
    public sealed record WeatherRiskInput(
        short BeaufortNumber,
        decimal Rainfall1hMm,
        decimal? VisibilityKm);

    /// Kết quả đánh giá của một yếu tố thời tiết
    public sealed record FactorRiskEvaluation(
        string Factor,
        string RiskLevel,
        decimal? MeasuredValue,
        bool HasMeasurement);

    /// Kết quả tổng hợp của một lần đánh giá thời tiết
    public sealed record WeatherRiskEvaluation(
        FactorRiskEvaluation Wind,
        FactorRiskEvaluation Rain,
        FactorRiskEvaluation Visibility,
        string FinalRiskLevel,
        string DominantFactor,
        string Summary);
}
