using PORMS.Infrastructure.Weather;
using Xunit;

namespace PORMS.Tests.Unit;

public class RiskEngineTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.2, 0)]
    [InlineData(0.3, 1)]
    [InlineData(5.0, 3)]
    [InlineData(10.7, 5)]
    [InlineData(10.8, 6)]
    [InlineData(17.1, 7)]
    [InlineData(17.2, 8)]
    [InlineData(24.4, 9)]
    [InlineData(24.5, 10)]
    [InlineData(32.6, 11)]
    [InlineData(32.7, 12)]
    public void ConvertToBeaufort_UsesWmoBoundaryValues(decimal windSpeedMs, int expected)
    {
        var actual = OpenWeatherService.ConvertToBeaufort(windSpeedMs);

        Assert.Equal(expected, actual);
    }
}
