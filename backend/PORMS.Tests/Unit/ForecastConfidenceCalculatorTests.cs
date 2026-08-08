using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class ForecastConfidenceCalculatorTests
{
    [Fact]
    public void Calculate_UsesOnlyRealDataAndFlagsRepeatedMismatch()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = new List<ForecastEvaluationRowReadModel>
        {
            Row(now.AddDays(-1), "HIGH", "CRITICAL", 1, "MATCHED"),
            Row(now.AddDays(-2), "LOW", "HIGH", 2, "MATCHED"),
            Row(now.AddDays(-3), "MEDIUM", "HIGH", 1, "MATCHED"),
            Row(now.AddDays(-4), "LOW", "LOW", 0, "MATCHED"),
            Row(now.AddDays(-5), "MEDIUM", "MEDIUM", 0, "MATCHED"),
            Row(now.AddDays(-6), "LOW", "LOW", 0, "MATCHED_DEMO")
        };

        var summary = ForecastConfidenceCalculator.Calculate(rows);

        Assert.Equal(5, summary.MatchedActualPoints);
        Assert.Equal(40m, summary.ConfidencePct);
        Assert.Equal("LOW", summary.ConfidenceLevel);
        Assert.Equal(3, summary.ConsecutiveMismatchCount);
        Assert.Equal(3, summary.DangerousUnderestimateCount);
        Assert.True(summary.InterventionRequired);
        Assert.Equal(40m, summary.HorizonConfidence.Single(item => item.HorizonDay == 1).ConfidencePct);
        Assert.Equal(5, summary.HorizonConfidence.Single(item => item.HorizonDay == 1).SampleCount);
        Assert.Null(summary.HorizonConfidence.Single(item => item.HorizonDay == 2).ConfidencePct);
    }

    [Fact]
    public void Calculate_MarksSmallSamplesAsInsufficient()
    {
        var rows = new[]
        {
            Row(DateTimeOffset.UtcNow.AddDays(-1), "LOW", "LOW", 0, "MATCHED"),
            Row(DateTimeOffset.UtcNow.AddDays(1), "LOW", null, null, "FUTURE")
        };

        var summary = ForecastConfidenceCalculator.Calculate(rows);

        Assert.Equal(100m, summary.ConfidencePct);
        Assert.Equal("INSUFFICIENT", summary.ConfidenceLevel);
        Assert.False(summary.InterventionRequired);
        Assert.Equal(1, summary.EligiblePastPoints);
    }

    private static ForecastEvaluationRowReadModel Row(
        DateTimeOffset plannedAt,
        string forecastRisk,
        string? actualRisk,
        int? riskError,
        string status) => new(
            "Forecast plan",
            "DNTSA",
            "Cảng Tiên Sa",
            1,
            plannedAt,
            actualRisk is null ? null : plannedAt,
            8m,
            actualRisk is null ? null : 8m,
            actualRisk is null ? null : 0m,
            2m,
            actualRisk is null ? null : 2m,
            actualRisk is null ? null : 0m,
            8m,
            actualRisk is null ? null : 8m,
            actualRisk is null ? null : 0m,
            forecastRisk,
            actualRisk,
            riskError,
            status == "MATCHED_DEMO" ? "DEMO_BACKFILL" : "OPENWEATHER",
            status);
}
