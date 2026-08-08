using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

/// <summary>
/// Đánh giá độ tin cậy thực nghiệm từ forecast đã được đối chiếu với dữ liệu thật.
/// Dữ liệu mô phỏng/backfill không được dùng để tính độ tin cậy.
/// </summary>
public static class ForecastConfidenceCalculator
{
    private const int MinimumReliableSampleSize = 3;

    public static ForecastEvaluationSummaryResponse Calculate(
        IReadOnlyList<ForecastEvaluationRowReadModel> rows)
    {
        var eligiblePastRows = rows
            .Where(item => item.Status != "FUTURE")
            .ToList();
        var realMatchedRows = rows
            .Where(item => item.Status == "MATCHED" && item.ActualObservedAt is not null)
            .OrderByDescending(item => item.PlannedAt)
            .ToList();
        var riskMatchedCount = realMatchedRows.Count(item => item.RiskScoreError == 0);
        var confidencePct = realMatchedRows.Count == 0
            ? (decimal?)null
            : Math.Round(riskMatchedCount * 100m / realMatchedRows.Count, 1);
        var consecutiveMismatchCount = realMatchedRows
            .TakeWhile(item => item.RiskScoreError is > 0)
            .Count();
        var dangerousUnderestimateCount = realMatchedRows
            .Take(5)
            .Count(IsDangerousUnderestimate);

        var interventionRequired =
            realMatchedRows.Count >= MinimumReliableSampleSize && consecutiveMismatchCount >= 3 ||
            realMatchedRows.Count >= 5 && confidencePct < 70m ||
            realMatchedRows.Count >= 2 && dangerousUnderestimateCount >= 2;

        var confidenceLevel = ConfidenceLevel(confidencePct, realMatchedRows.Count);
        var (message, actions) = BuildInterventionGuidance(
            interventionRequired,
            confidenceLevel,
            realMatchedRows.Count,
            consecutiveMismatchCount,
            dangerousUnderestimateCount);

        return new ForecastEvaluationSummaryResponse
        {
            TotalForecastPoints = rows.Count,
            EligiblePastPoints = eligiblePastRows.Count,
            MatchedActualPoints = realMatchedRows.Count,
            MatchRatePct = eligiblePastRows.Count == 0
                ? 0
                : Math.Round(realMatchedRows.Count * 100m / eligiblePastRows.Count, 1),
            ConfidencePct = confidencePct,
            ConfidenceLevel = confidenceLevel,
            RiskMatchRatePct = confidencePct ?? 0,
            ConsecutiveMismatchCount = consecutiveMismatchCount,
            DangerousUnderestimateCount = dangerousUnderestimateCount,
            InterventionRequired = interventionRequired,
            InterventionMessage = message,
            RecommendedActions = actions,
            AvgWindMae = Average(realMatchedRows.Select(item => item.WindAbsError)),
            AvgRainMae = Average(realMatchedRows.Select(item => item.RainAbsError)),
            AvgVisibilityMae = Average(realMatchedRows.Select(item => item.VisibilityAbsError)),
            AvgRiskScoreError = Average(realMatchedRows.Select(item =>
                item.RiskScoreError.HasValue ? (decimal?)item.RiskScoreError.Value : null)),
            HorizonConfidence = Enumerable.Range(1, 5)
                .Select(horizonDay => HorizonConfidence(realMatchedRows, horizonDay))
                .ToList()
        };
    }

    private static ForecastHorizonConfidenceResponse HorizonConfidence(
        IReadOnlyList<ForecastEvaluationRowReadModel> realMatchedRows,
        int horizonDay)
    {
        var rows = realMatchedRows
            .Where(item => item.SnapshotNumber == horizonDay)
            .ToList();
        var matchedRiskCount = rows.Count(item => item.RiskScoreError == 0);
        var confidencePct = rows.Count == 0
            ? (decimal?)null
            : Math.Round(matchedRiskCount * 100m / rows.Count, 1);

        return new ForecastHorizonConfidenceResponse
        {
            HorizonDay = horizonDay,
            SampleCount = rows.Count,
            ConfidencePct = confidencePct,
            ConfidenceLevel = ConfidenceLevel(confidencePct, rows.Count),
            AvgWindMae = Average(rows.Select(item => item.WindAbsError)),
            AvgRainMae = Average(rows.Select(item => item.RainAbsError)),
            AvgVisibilityMae = Average(rows.Select(item => item.VisibilityAbsError))
        };
    }

    private static bool IsDangerousUnderestimate(ForecastEvaluationRowReadModel row)
    {
        if (row.ActualRiskLevel is null) return false;
        return RiskScore(row.ActualRiskLevel) > RiskScore(row.ForecastRiskLevel);
    }

    private static string ConfidenceLevel(decimal? confidencePct, int sampleCount)
    {
        if (sampleCount < MinimumReliableSampleSize) return "INSUFFICIENT";
        if (confidencePct >= 85m) return "HIGH";
        if (confidencePct >= 70m) return "MEDIUM";
        return "LOW";
    }

    private static (string Message, IReadOnlyList<string> Actions) BuildInterventionGuidance(
        bool interventionRequired,
        string confidenceLevel,
        int sampleCount,
        int consecutiveMismatchCount,
        int dangerousUnderestimateCount)
    {
        if (sampleCount < MinimumReliableSampleSize)
        {
            return (
                $"Mới có {sampleCount} mốc dữ liệu thật; cần ít nhất {MinimumReliableSampleSize} mốc để kết luận độ tin cậy.",
                new[] { "Tiếp tục thu thập dữ liệu thật và chưa tự động thay đổi ngưỡng rủi ro." });
        }

        if (interventionRequired)
        {
            return (
                $"Cần rà soát: sai liên tiếp {consecutiveMismatchCount} lần và có {dangerousUnderestimateCount} lần đánh giá thấp nguy hiểm trong 5 mốc gần nhất.",
                new[]
                {
                    "Kiểm tra tính liên tục, thời điểm và đơn vị của dữ liệu OpenWeather.",
                    "Admin và quản lý cảng rà soát ngưỡng rủi ro trước khi phê duyệt điều chỉnh.",
                    "Trong lúc rà soát, giữ phương án vận hành theo mức an toàn cao hơn; không tự động hạ mức cảnh báo."
                });
        }

        return (
            confidenceLevel == "HIGH"
                ? "Dự báo đang khớp tốt với mức rủi ro thực tế; tiếp tục theo dõi định kỳ."
                : "Dự báo có thể sử dụng để tham khảo kế hoạch nhưng cần tiếp tục theo dõi sai số.",
            new[] { "Tiếp tục đối chiếu hằng ngày và rà soát nếu xuất hiện chuỗi sai liên tiếp." });
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var concreteValues = values
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        return concreteValues.Count == 0
            ? null
            : Math.Round(concreteValues.Average(), 2);
    }

    private static int RiskScore(string riskLevel) => riskLevel switch
    {
        "LOW" => 1,
        "MEDIUM" => 2,
        "HIGH" => 3,
        "CRITICAL" => 4,
        _ => 0
    };
}
