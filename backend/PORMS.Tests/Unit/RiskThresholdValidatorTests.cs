using System;
using System.Collections.Generic;
using System.Text;
using PORMS.API.Services;
using Xunit;

namespace PORMS.Tests.Unit
{
    public sealed class RiskThresholdValidatorTests
    {
        private readonly RiskThresholdValidator _validator = new();

        [Fact]
        public void ValidateRows_WithValidInput_NormalizesValues()
        {
            var rows = new[]
            {
            new RiskThresholdCandidate(
                2,
                " wind ",
                " medium ",
                ">=",
                6,
                "beaufort",
                "  Gió mức trung bình  ",
                true)
        };

            var result = _validator.ValidateRows(rows);

            Assert.True(result.IsValid);
            var threshold = Assert.Single(result.ValidRows);

            Assert.Equal("WIND", threshold.Factor);
            Assert.Equal("MEDIUM", threshold.RiskLevel);
            Assert.Equal("GTE", threshold.ComparisonOperator);
            Assert.Equal("Beaufort", threshold.Unit);
            Assert.Equal("Gió mức trung bình", threshold.Description);
        }

        [Fact]
        public void ValidateRows_WithInvalidWindValues_ReturnsDetailedErrors()
        {
            var rows = new[]
            {
            new RiskThresholdCandidate(
                2,
                "WIND",
                "HIGH",
                ">",
                12.5m,
                "m/s",
                null,
                true)
        };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors,
                error => error.Column == "ComparisonOperator");
            Assert.Contains(result.Errors,
                error => error.Column == "ThresholdValue");
            Assert.Contains(result.Errors,
                error => error.Column == "Unit");
        }

        [Fact]
        public void ValidateRows_WithDuplicateFactorAndLevel_ReturnsError()
        {
            var rows = new[]
            {
            new RiskThresholdCandidate(
                2, "RAIN", "HIGH", "GTE", 25, "mm/h", null, true),

            new RiskThresholdCandidate(
                3, "RAIN", "HIGH", "GTE", 30, "mm/h", null, true)
        };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);

            var duplicateError = Assert.Single(
                result.Errors,
                error => error.Column == "Factor/RiskLevel");

            Assert.Equal(3, duplicateError.RowNumber);
            Assert.Contains("dòng 2", duplicateError.Message);
        }

        [Fact]
        public void ValidateRows_WithUnsupportedVersion_ReturnsError()
        {
            var rows = new[]
            {
            new RiskThresholdCandidate(
                2,
                "RAIN",
                "MEDIUM",
                "GTE",
                10,
                "mm/h",
                null,
                true,
                Version: 2)
        };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Column == "Version");
        }

        [Fact]
        public void ValidateConfiguration_WithCompleteConfiguration_ReturnsNoErrors()
        {
            var rowResult = _validator.ValidateRows(CreateValidConfiguration());

            Assert.True(rowResult.IsValid);

            var configurationErrors =
                _validator.ValidateConfiguration(rowResult.ValidRows);

            Assert.Empty(configurationErrors);
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidAscendingOrder_ReturnsError()
        {
            var candidates = CreateValidConfiguration();
            var highWindIndex = candidates.FindIndex(row =>
                row.Factor == "WIND" && row.RiskLevel == "HIGH");

            // HIGH thấp hơn MEDIUM làm thứ tự ngưỡng gió không hợp lệ.
            candidates[highWindIndex] = candidates[highWindIndex] with
            {
                ThresholdValue = 5
            };

            var rowResult = _validator.ValidateRows(candidates);
            var configurationErrors =
                _validator.ValidateConfiguration(rowResult.ValidRows);

            Assert.Contains(configurationErrors, error =>
                error.Column == "Configuration" &&
                error.Message.Contains("MEDIUM < HIGH < CRITICAL"));
        }

        [Fact]
        public void ValidateConfiguration_WithInvalidVisibilityBoundary_ReturnsError()
        {
            var candidates = CreateValidConfiguration();
            var lowVisibilityIndex = candidates.FindIndex(row =>
                row.Factor == "VISIBILITY" && row.RiskLevel == "LOW");

            candidates[lowVisibilityIndex] = candidates[lowVisibilityIndex] with
            {
                ThresholdValue = 12
            };

            var rowResult = _validator.ValidateRows(candidates);
            var configurationErrors =
                _validator.ValidateConfiguration(rowResult.ValidRows);

            Assert.Contains(configurationErrors, error =>
                error.Message.Contains("cùng điểm chuyển mức"));
        }

        [Fact]
        public void ValidateConfiguration_WhenFactorIsDisabled_ReturnsError()
        {
            var candidates = CreateValidConfiguration();

            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Factor == "RAIN")
                {
                    candidates[index] = candidates[index] with
                    {
                        IsEnabled = false
                    };
                }
            }

            var rowResult = _validator.ValidateRows(candidates);
            var configurationErrors =
                _validator.ValidateConfiguration(rowResult.ValidRows);

            Assert.Contains(configurationErrors, error =>
                error.Column == "IsEnabled" &&
                error.Message.Contains("RAIN"));
        }

        [Fact]
        public void ValidateConfiguration_WhenThresholdIsMissing_ReturnsError()
        {
            var candidates = CreateValidConfiguration();

            candidates.RemoveAll(row =>
                row.Factor == "VISIBILITY" &&
                row.RiskLevel == "CRITICAL");

            var rowResult = _validator.ValidateRows(candidates);
            var configurationErrors =
                _validator.ValidateConfiguration(rowResult.ValidRows);

            Assert.Contains(configurationErrors, error =>
                error.Message.Contains("VISIBILITY:CRITICAL"));
        }

        private static List<RiskThresholdCandidate> CreateValidConfiguration()
        {
            var rowNumber = 2;

            return
            [
                New("WIND", "LOW", "GTE", 0, "Beaufort"),
            New("WIND", "MEDIUM", "GTE", 6, "Beaufort"),
            New("WIND", "HIGH", "GTE", 8, "Beaufort"),
            New("WIND", "CRITICAL", "GTE", 10, "Beaufort"),

            New("RAIN", "LOW", "GTE", 0, "mm/h"),
            New("RAIN", "MEDIUM", "GTE", 10, "mm/h"),
            New("RAIN", "HIGH", "GTE", 25, "mm/h"),
            New("RAIN", "CRITICAL", "GTE", 50, "mm/h"),

            New("VISIBILITY", "LOW", "GTE", 10, "km"),
            New("VISIBILITY", "MEDIUM", "LTE", 10, "km"),
            New("VISIBILITY", "HIGH", "LTE", 5, "km"),
            New("VISIBILITY", "CRITICAL", "LTE", 1, "km")
            ];

            RiskThresholdCandidate New(
                string factor,
                string riskLevel,
                string comparisonOperator,
                decimal thresholdValue,
                string unit)
            {
                return new RiskThresholdCandidate(
                    rowNumber++,
                    factor,
                    riskLevel,
                    comparisonOperator,
                    thresholdValue,
                    unit,
                    null,
                    true);
            }
        }
    }
}
