using System.Text.Json;
using PORMS.API.Services;
using Xunit;

namespace PORMS.Tests.Unit;

public sealed class OpenWeatherForecastParserTests
{
    [Fact]
    public void ParsesFreeFiveDayForecastIntoDailyRows()
    {
        const string payload = """
            {
              "list": [
                {
                  "dt": 1783036800,
                  "main": { "temp": 27.5, "temp_min": 26.1, "temp_max": 29.2, "humidity": 80, "pressure": 1006 },
                  "visibility": 9000,
                  "weather": [{ "id": 500, "description": "mua nhe" }],
                  "wind": { "speed": 6.4, "gust": 9.1, "deg": 120 },
                  "rain": { "3h": 1.2 },
                  "pop": 0.45
                },
                {
                  "dt": 1783047600,
                  "main": { "temp": 28.5, "temp_min": 27.2, "temp_max": 31.4, "humidity": 78, "pressure": 1004 },
                  "visibility": 6000,
                  "weather": [{ "id": 501, "description": "mua vua" }],
                  "wind": { "speed": 8.2, "gust": 12.3, "deg": 130 },
                  "rain": { "3h": 2.8 },
                  "pop": 0.70
                },
                {
                  "dt": 1783123200,
                  "main": { "temp": 29.1, "temp_min": 25.7, "temp_max": 30.0, "humidity": 76, "pressure": 1008 },
                  "visibility": 10000,
                  "weather": [{ "id": 801, "description": "it may" }],
                  "wind": { "speed": 5.1, "deg": 90 },
                  "pop": 0.10
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(payload);
        var days = OpenWeatherForecastParser.ParseDailyForecast(document.RootElement, 5);

        Assert.Equal(2, days.Count);
        Assert.Equal(26.1m, days[0].TemperatureMinC);
        Assert.Equal(31.4m, days[0].TemperatureMaxC);
        Assert.Equal(8.2m, days[0].WindSpeedMs);
        Assert.Equal(12.3m, days[0].WindGustMs);
        Assert.Equal(4.0m, days[0].RainMm);
        Assert.Equal(70, days[0].PopPct);
        Assert.Equal("mua vua", days[0].WeatherDescription);
        Assert.Equal(79, days[0].HumidityPct);
        Assert.Equal(1005m, days[0].PressureHpa);
        Assert.Equal(6m, days[0].VisibilityKm);
    }
}
