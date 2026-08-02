using Microsoft.AspNetCore.Http;

namespace PORMS.API.Services
{
    /// Kiểm tra và chuẩn hóa cấu hình ngưỡng trước khi lưu vào database
    /// Dùng chung cho nhập Excel và chỉnh sửa thủ công
    public sealed class RiskThresholdValidator
    {
        private static readonly HashSet<string> AllowedFactors =
            ["WIND", "RAIN", "VISIBILITY"];

        private static readonly HashSet<string> AllowedRiskLevels =
            ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

        private static readonly Dictionary<string, string> ExpectedUnits = new()
        {
            ["WIND"] = "Beaufort",
            ["RAIN"] = "mm/h",
            ["VISIBILITY"] = "km"
        };

        public RiskThresholdValidationResult ValidateRows(
            IReadOnlyList<RiskThresholdCandidate> candidates)
        {
            var validRows = new List<ValidatedRiskThreshold>();
            var errors = new List<RiskThresholdValidationError>();
            var seenKeys = new Dictionary<string, int>(StringComparer.Ordinal);

            if (candidates.Count == 0)
            {
                errors.Add(new(0, "File", "File không chứa dòng dữ liệu nào."));
                return new(validRows, errors);
            }

            foreach (var candidate in candidates)
            {
                var errorCountBeforeRow = errors.Count;
                var factor = NormalizeAllowed(candidate.Factor, AllowedFactors);
                var riskLevel = NormalizeAllowed(candidate.RiskLevel, AllowedRiskLevels);
                var comparisonOperator = NormalizeOperator(candidate.ComparisonOperator);
                var description = NormalizeOptional(candidate.Description);

                if (factor is null)
                    errors.Add(new(candidate.RowNumber, "Factor",
                        "Factor chỉ chấp nhận WIND, RAIN hoặc VISIBILITY."));

                if (riskLevel is null)
                    errors.Add(new(candidate.RowNumber, "RiskLevel",
                        "RiskLevel chỉ chấp nhận LOW, MEDIUM, HIGH hoặc CRITICAL."));

                if (comparisonOperator is null)
                    errors.Add(new(candidate.RowNumber, "ComparisonOperator",
                        "Toán tử chỉ chấp nhận >=, <=, GTE hoặc LTE."));

                if (candidate.ThresholdValue is null)
                {
                    errors.Add(new(candidate.RowNumber, "ThresholdValue",
                        "ThresholdValue là bắt buộc và phải là số."));
                }
                else
                {
                    ValidateThresholdValue(candidate, factor, errors);
                }

                var unit = ValidateAndNormalizeUnit(candidate, factor, errors);

                if (description?.Length > 1000)
                    errors.Add(new(candidate.RowNumber, "Description",
                        "Description không được vượt quá 1000 ký tự."));

                if (candidate.IsEnabled is null)
                    errors.Add(new(candidate.RowNumber, "IsEnabled",
                        "IsEnabled phải là TRUE hoặc FALSE."));

                if (candidate.Version != 1)
                    errors.Add(new(candidate.RowNumber, "Version",
                        "Phiên bản hiện tại chỉ hỗ trợ Version = 1."));

                ValidateOperatorForFactor(
                    candidate.RowNumber,
                    factor,
                    riskLevel,
                    comparisonOperator,
                    errors);

                if (factor is not null && riskLevel is not null)
                {
                    var key = $"{factor}:{riskLevel}";

                    if (seenKeys.TryGetValue(key, out var firstRow))
                    {
                        errors.Add(new(candidate.RowNumber, "Factor/RiskLevel",
                            $"Ngưỡng {key} đã xuất hiện tại dòng {firstRow}."));
                    }
                    else
                    {
                        seenKeys[key] = candidate.RowNumber;
                    }
                }

                if (errors.Count == errorCountBeforeRow)
                {
                    validRows.Add(new ValidatedRiskThreshold(
                        candidate.RowNumber,
                        factor!,
                        riskLevel!,
                        comparisonOperator!,
                        candidate.ThresholdValue!.Value,
                        unit!,
                        description,
                        candidate.IsEnabled!.Value,
                        1));
                }
            }

            return new(validRows, errors);
        }

        public IReadOnlyList<RiskThresholdValidationError> ValidateConfiguration(
            IReadOnlyList<ValidatedRiskThreshold> thresholds)
        {
            var errors = new List<RiskThresholdValidationError>();

            var duplicateGroups = thresholds
                .GroupBy(row => $"{row.Factor}:{row.RiskLevel}")
                .Where(group => group.Count() > 1);

            foreach (var duplicate in duplicateGroups)
            {
                errors.Add(new(
                    FindSourceRow(duplicate),
                    "Configuration",
                    $"Cấu hình chứa nhiều ngưỡng {duplicate.Key}."));
            }

            var thresholdMap = thresholds
                .GroupBy(row => $"{row.Factor}:{row.RiskLevel}")
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var factor in AllowedFactors)
            {
                var factorRows = new List<ValidatedRiskThreshold>();

                foreach (var riskLevel in AllowedRiskLevels)
                {
                    if (thresholdMap.TryGetValue($"{factor}:{riskLevel}", out var row))
                    {
                        factorRows.Add(row);
                    }
                    else
                    {
                        errors.Add(new(0, "Configuration",
                            $"Thiếu ngưỡng {factor}:{riskLevel}."));
                    }
                }

                if (factorRows.Count != AllowedRiskLevels.Count)
                    continue;

                if (factorRows.All(row => !row.IsEnabled))
                {
                    errors.Add(new(FindSourceRow(factorRows), "IsEnabled",
                        $"Yếu tố {factor} phải có ít nhất một ngưỡng đang bật."));
                }

                ValidateThresholdOrder(factor, factorRows, errors);
            }

            return errors;
        }

        private static void ValidateThresholdValue(
            RiskThresholdCandidate candidate,
            string? factor,
            ICollection<RiskThresholdValidationError> errors)
        {
            var value = candidate.ThresholdValue!.Value;

            if (value < 0)
                errors.Add(new(candidate.RowNumber, "ThresholdValue",
                    "ThresholdValue không được là số âm."));

            if (value != decimal.Round(value, 3))
                errors.Add(new(candidate.RowNumber, "ThresholdValue",
                    "ThresholdValue chỉ được có tối đa 3 chữ số thập phân."));

            if (value > 999_999_999.999m)
                errors.Add(new(candidate.RowNumber, "ThresholdValue",
                    "ThresholdValue vượt quá giới hạn lưu trữ của database."));

            if (factor == "WIND" && (value > 12 || value != decimal.Truncate(value)))
                errors.Add(new(candidate.RowNumber, "ThresholdValue",
                    "Ngưỡng gió Beaufort phải là số nguyên từ 0 đến 12."));
        }

        private static string? ValidateAndNormalizeUnit(
            RiskThresholdCandidate candidate,
            string? factor,
            ICollection<RiskThresholdValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(candidate.Unit))
            {
                errors.Add(new(candidate.RowNumber, "Unit", "Unit là bắt buộc."));
                return null;
            }

            var unit = candidate.Unit.Trim();

            if (factor is null)
                return unit;

            var expectedUnit = ExpectedUnits[factor];

            if (!string.Equals(unit, expectedUnit, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new(candidate.RowNumber, "Unit",
                    $"Đơn vị của {factor} phải là {expectedUnit}."));
            }

            return expectedUnit;
        }

        private static void ValidateOperatorForFactor(
            int rowNumber,
            string? factor,
            string? riskLevel,
            string? comparisonOperator,
            ICollection<RiskThresholdValidationError> errors)
        {
            if (factor is null || riskLevel is null || comparisonOperator is null)
                return;

            var expectedOperator = factor switch
            {
                "WIND" or "RAIN" => "GTE",
                "VISIBILITY" when riskLevel == "LOW" => "GTE",
                "VISIBILITY" => "LTE",
                _ => string.Empty
            };

            if (comparisonOperator != expectedOperator)
            {
                errors.Add(new(rowNumber, "ComparisonOperator",
                    $"Ngưỡng {factor}:{riskLevel} phải sử dụng toán tử {expectedOperator}."));
            }
        }

        private static void ValidateThresholdOrder(
            string factor,
            IReadOnlyList<ValidatedRiskThreshold> rows,
            ICollection<RiskThresholdValidationError> errors)
        {
            var low = rows.Single(row => row.RiskLevel == "LOW");
            var medium = rows.Single(row => row.RiskLevel == "MEDIUM");
            var high = rows.Single(row => row.RiskLevel == "HIGH");
            var critical = rows.Single(row => row.RiskLevel == "CRITICAL");
            var sourceRow = FindSourceRow(rows);

            if (factor is "WIND" or "RAIN")
            {
                if (low.ThresholdValue != 0)
                    errors.Add(new(low.RowNumber, "ThresholdValue",
                        $"Ngưỡng LOW của {factor} phải bắt đầu từ 0."));

                if (!(medium.ThresholdValue < high.ThresholdValue &&
                      high.ThresholdValue < critical.ThresholdValue))
                {
                    errors.Add(new(sourceRow, "Configuration",
                        $"Ngưỡng {factor} phải tăng theo MEDIUM < HIGH < CRITICAL."));
                }

                return;
            }

            if (low.ThresholdValue != medium.ThresholdValue)
                errors.Add(new(sourceRow, "Configuration",
                    "Ngưỡng LOW và MEDIUM của VISIBILITY phải dùng cùng điểm chuyển mức."));

            if (!(medium.ThresholdValue > high.ThresholdValue &&
                  high.ThresholdValue > critical.ThresholdValue))
            {
                errors.Add(new(sourceRow, "Configuration",
                    "Ngưỡng VISIBILITY phải giảm theo MEDIUM > HIGH > CRITICAL."));
            }
        }

        private static string? NormalizeAllowed(
            string? value,
            HashSet<string> allowedValues)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim().ToUpperInvariant();
            return allowedValues.Contains(normalized) ? normalized : null;
        }

        private static string? NormalizeOperator(string? value)
        {
            return value?.Trim().ToUpperInvariant() switch
            {
                ">=" or "GTE" => "GTE",
                "<=" or "LTE" => "LTE",
                _ => null
            };
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static int FindSourceRow(
            IEnumerable<ValidatedRiskThreshold> rows) =>
            rows.FirstOrDefault(row => row.RowNumber > 0)?.RowNumber ?? 0;
    }

    public sealed record RiskThresholdCandidate(
        int RowNumber,
        string? Factor,
        string? RiskLevel,
        string? ComparisonOperator,
        decimal? ThresholdValue,
        string? Unit,
        string? Description,
        bool? IsEnabled,
        int Version = 1);

    public sealed record ValidatedRiskThreshold(
        int RowNumber,
        string Factor,
        string RiskLevel,
        string ComparisonOperator,
        decimal ThresholdValue,
        string Unit,
        string? Description,
        bool IsEnabled,
        int Version);

    public sealed record RiskThresholdValidationError(
        int RowNumber,
        string Column,
        string Message);

    public sealed record RiskThresholdValidationResult(
        IReadOnlyList<ValidatedRiskThreshold> ValidRows,
        IReadOnlyList<RiskThresholdValidationError> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }
}
