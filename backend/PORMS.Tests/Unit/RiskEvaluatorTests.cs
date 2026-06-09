using PORMS.Application.Services.RiskEngine;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using Xunit;

namespace PORMS.Tests.Unit;

public class RiskEvaluatorTests
{
    [Theory]
    [InlineData(10.7, RiskLevel.LOW)]
    [InlineData(10.8, RiskLevel.MEDIUM)]
    [InlineData(17.1, RiskLevel.MEDIUM)]
    [InlineData(17.2, RiskLevel.HIGH)]
    [InlineData(24.4, RiskLevel.HIGH)]
    [InlineData(24.5, RiskLevel.CRITICAL)]
    [InlineData(32.7, RiskLevel.CRITICAL)]
    [InlineData(100.0, RiskLevel.CRITICAL)]
    public void WindEvaluator_UsesConfiguredBoundaryValues(double windSpeedMs, RiskLevel expected)
    {
        var actual = WindEvaluator.Evaluate(windSpeedMs, DefaultThresholds);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WindEvaluator_NegativeInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WindEvaluator.Evaluate(-0.1, DefaultThresholds));
    }

    [Theory]
    [InlineData(null, RiskLevel.LOW)]
    [InlineData(0.0, RiskLevel.LOW)]
    [InlineData(9.9, RiskLevel.LOW)]
    [InlineData(10.0, RiskLevel.MEDIUM)]
    [InlineData(24.9, RiskLevel.MEDIUM)]
    [InlineData(25.0, RiskLevel.HIGH)]
    [InlineData(49.9, RiskLevel.HIGH)]
    [InlineData(50.0, RiskLevel.CRITICAL)]
    public void RainEvaluator_UsesConfiguredBoundaryValues(double? rainfall1hMm, RiskLevel expected)
    {
        var actual = RainEvaluator.Evaluate(rainfall1hMm, DefaultThresholds);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, RiskLevel.LOW)]
    [InlineData(10.0, RiskLevel.LOW)]
    [InlineData(9.9, RiskLevel.MEDIUM)]
    [InlineData(5.0, RiskLevel.MEDIUM)]
    [InlineData(4.9, RiskLevel.HIGH)]
    [InlineData(1.0, RiskLevel.HIGH)]
    [InlineData(0.9, RiskLevel.CRITICAL)]
    public void VisibilityEvaluator_UsesDescendingThresholds(double? visibilityKm, RiskLevel expected)
    {
        var actual = VisibilityEvaluator.Evaluate(visibilityKm, DefaultThresholds);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AggregatorService_ReturnsWorstRisk()
    {
        var actual = AggregatorService.Aggregate(
            RiskLevel.LOW,
            RiskLevel.HIGH,
            RiskLevel.MEDIUM);

        Assert.Equal(RiskLevel.HIGH, actual);
    }

    private static readonly IReadOnlyList<RiskThreshold> DefaultThresholds =
    [
        New(WeatherFactor.WIND, RiskLevel.LOW, 0m, 10.8m, "m/s"),
        New(WeatherFactor.WIND, RiskLevel.MEDIUM, 10.8m, 17.2m, "m/s"),
        New(WeatherFactor.WIND, RiskLevel.HIGH, 17.2m, 24.5m, "m/s"),
        New(WeatherFactor.WIND, RiskLevel.CRITICAL, 24.5m, null, "m/s"),
        New(WeatherFactor.RAIN, RiskLevel.LOW, 0m, 10m, "mm/h"),
        New(WeatherFactor.RAIN, RiskLevel.MEDIUM, 10m, 25m, "mm/h"),
        New(WeatherFactor.RAIN, RiskLevel.HIGH, 25m, 50m, "mm/h"),
        New(WeatherFactor.RAIN, RiskLevel.CRITICAL, 50m, null, "mm/h"),
        New(WeatherFactor.VISIBILITY, RiskLevel.CRITICAL, 0m, 1m, "km"),
        New(WeatherFactor.VISIBILITY, RiskLevel.HIGH, 1m, 5m, "km"),
        New(WeatherFactor.VISIBILITY, RiskLevel.MEDIUM, 5m, 10m, "km"),
        New(WeatherFactor.VISIBILITY, RiskLevel.LOW, 10m, null, "km")
    ];

    private static RiskThreshold New(
        WeatherFactor factor,
        RiskLevel riskLevel,
        decimal minValue,
        decimal? maxValue,
        string unit)
        => new()
        {
            Factor = factor,
            RiskLevel = riskLevel,
            MinValue = minValue,
            MaxValue = maxValue,
            Unit = unit,
            IsActive = true
        };
}
