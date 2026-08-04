using System;
using System.Collections.Generic;
using System.Text;
using PORMS.Infrastructure.Services;
using Xunit;

namespace PORMS.Tests.Unit
{
    /// Kiểm tra Risk Engine bằng dữ liệu thuần, không cần database
    public sealed class RiskEngineTests
    {
        private readonly RiskThresholdEvaluator _evaluator = new();

        [Fact]
        public void Evaluate_WithLowWeather_ReturnsLowRisk()
        {
            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 5,
                    Rainfall1hMm: 9m,
                    VisibilityKm: 11m),
                CreateStandardThresholds());

            Assert.Equal("LOW", result.Wind.RiskLevel);
            Assert.Equal("LOW", result.Rain.RiskLevel);
            Assert.Equal("LOW", result.Visibility.RiskLevel);
            Assert.Equal("LOW", result.FinalRiskLevel);
        }

        [Fact]
        public void Evaluate_WithCustomThreshold_UsesConfiguredValue()
        {
            var thresholds = CreateStandardThresholds()
                .Select(rule =>
                    rule.Factor == "WIND" &&
                    rule.RiskLevel == "MEDIUM"
                        ? rule with { ThresholdValue = 4m }
                        : rule)
                .ToList();

            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 5,
                    Rainfall1hMm: 0m,
                    VisibilityKm: 20m),
                thresholds);

            // Giá trị 5 sẽ là LOW theo cấu hình cũ nhưng là MEDIUM theo cấu hình mới
            Assert.Equal("MEDIUM", result.Wind.RiskLevel);
            Assert.Equal("MEDIUM", result.FinalRiskLevel);
            Assert.Equal("WIND", result.DominantFactor);
        }

        [Fact]
        public void Evaluate_SelectsHighestRiskAcrossFactors()
        {
            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 8,
                    Rainfall1hMm: 50m,
                    VisibilityKm: 8m),
                CreateStandardThresholds());

            Assert.Equal("HIGH", result.Wind.RiskLevel);
            Assert.Equal("CRITICAL", result.Rain.RiskLevel);
            Assert.Equal("MEDIUM", result.Visibility.RiskLevel);
            Assert.Equal("CRITICAL", result.FinalRiskLevel);
            Assert.Equal("RAIN", result.DominantFactor);
        }

        [Fact]
        public void Evaluate_VisibilityAtSharedBoundary_ReturnsMedium()
        {
            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 0,
                    Rainfall1hMm: 0m,
                    VisibilityKm: 10m),
                CreateStandardThresholds());

            // LOW >= 10 và MEDIUM <= 10 cùng khớp tại 10 km
            // Risk Engine phải chọn mức có độ nghiêm trọng cao hơn
            Assert.Equal("MEDIUM", result.Visibility.RiskLevel);
            Assert.Equal("MEDIUM", result.FinalRiskLevel);
            Assert.Equal("VISIBILITY", result.DominantFactor);
        }

        [Fact]
        public void Evaluate_VisibilityAtCriticalBoundary_ReturnsCritical()
        {
            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 0,
                    Rainfall1hMm: 0m,
                    VisibilityKm: 1m),
                CreateStandardThresholds());

            Assert.Equal("CRITICAL", result.Visibility.RiskLevel);
            Assert.Equal("CRITICAL", result.FinalRiskLevel);
        }

        [Fact]
        public void Evaluate_WithoutVisibility_DoesNotTreatItAsZero()
        {
            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 0,
                    Rainfall1hMm: 0m,
                    VisibilityKm: null),
                CreateStandardThresholds());

            Assert.False(result.Visibility.HasMeasurement);
            Assert.Null(result.Visibility.MeasuredValue);
            Assert.Equal("LOW", result.Visibility.RiskLevel);
            Assert.Equal("LOW", result.FinalRiskLevel);
            Assert.Contains("không có dữ liệu", result.Summary);
            Assert.DoesNotContain("tầm nhìn 0 km", result.Summary);
        }

        [Fact]
        public void Evaluate_WithDisabledRule_SkipsThatRule()
        {
            var thresholds = CreateStandardThresholds()
                .Select(rule =>
                    rule.Factor == "WIND" &&
                    rule.RiskLevel == "HIGH"
                        ? rule with { IsEnabled = false }
                        : rule)
                .ToList();

            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 9,
                    Rainfall1hMm: 0m,
                    VisibilityKm: 20m),
                thresholds);

            Assert.Equal("MEDIUM", result.Wind.RiskLevel);
            Assert.Equal("MEDIUM", result.FinalRiskLevel);
        }

        [Fact]
        public void Evaluate_WithSymbolOperators_NormalizesOperators()
        {
            var thresholds = CreateStandardThresholds()
                .Select(rule => rule with
                {
                    ComparisonOperator =
                        rule.ComparisonOperator == "GTE"
                            ? ">="
                            : "<="
                })
                .ToList();

            var result = _evaluator.Evaluate(
                new WeatherRiskInput(
                    BeaufortNumber: 10,
                    Rainfall1hMm: 0m,
                    VisibilityKm: 20m),
                thresholds);

            Assert.Equal("CRITICAL", result.Wind.RiskLevel);
            Assert.Equal("CRITICAL", result.FinalRiskLevel);
        }

        [Fact]
        public void Evaluate_WithDuplicateThreshold_ThrowsConfigurationError()
        {
            var thresholds = CreateStandardThresholds();
            thresholds.Add(thresholds[0]);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                _evaluator.Evaluate(
                    new WeatherRiskInput(0, 0m, 20m),
                    thresholds));

            Assert.Contains(
                "đúng một threshold",
                exception.Message);
        }

        [Fact]
        public void Evaluate_WithWrongOperatorDirection_ThrowsConfigurationError()
        {
            var thresholds = CreateStandardThresholds()
                .Select(rule =>
                    rule.Factor == "WIND" &&
                    rule.RiskLevel == "HIGH"
                        ? rule with { ComparisonOperator = "LTE" }
                        : rule)
                .ToList();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                _evaluator.Evaluate(
                    new WeatherRiskInput(8, 0m, 20m),
                    thresholds));

            Assert.Contains(
                "WIND chỉ hỗ trợ toán tử GTE",
                exception.Message);
        }

        [Fact]
        public void Evaluate_WithNegativeWeatherValue_ThrowsInputError()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _evaluator.Evaluate(
                    new WeatherRiskInput(
                        BeaufortNumber: 0,
                        Rainfall1hMm: -1m,
                        VisibilityKm: 20m),
                    CreateStandardThresholds()));
        }

        /// Cấu hình chuẩn tương ứng với seed data hiện tại
        private static List<RiskThresholdRule> CreateStandardThresholds()
        {
            return
            [
                new("WIND", "LOW", "GTE", 0m, true),
            new("WIND", "MEDIUM", "GTE", 6m, true),
            new("WIND", "HIGH", "GTE", 8m, true),
            new("WIND", "CRITICAL", "GTE", 10m, true),

            new("RAIN", "LOW", "GTE", 0m, true),
            new("RAIN", "MEDIUM", "GTE", 10m, true),
            new("RAIN", "HIGH", "GTE", 25m, true),
            new("RAIN", "CRITICAL", "GTE", 50m, true),

            new("VISIBILITY", "LOW", "GTE", 10m, true),
            new("VISIBILITY", "MEDIUM", "LTE", 10m, true),
            new("VISIBILITY", "HIGH", "LTE", 5m, true),
            new("VISIBILITY", "CRITICAL", "LTE", 1m, true)
            ];
        }
    }
}
