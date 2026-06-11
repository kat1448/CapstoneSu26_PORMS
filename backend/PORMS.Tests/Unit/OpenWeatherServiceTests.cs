using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PORMS.Domain.Entities;
using PORMS.Infrastructure.Weather;
using Xunit;

namespace PORMS.Tests.Unit;

public class OpenWeatherServiceTests
{
    [Fact]
    public async Task FetchCurrentWeatherAsync_NormalizesOpenWeatherPayload()
    {
        const string responseBody = """
            {
              "wind": { "speed": 10.8, "deg": 95, "gust": 12.25 },
              "main": { "temp": 29.51, "humidity": 78, "pressure": 1008 },
              "visibility": 8000,
              "weather": [{ "id": 801, "description": "few clouds", "icon": "02d" }],
              "dt": 1779445800
            }
            """;

        var service = CreateService(responseBody);
        var port = new Port
        {
            Id = Guid.NewGuid(),
            Code = "DNTSA",
            Latitude = 16.12m,
            Longitude = 108.21m
        };

        var reading = await service.FetchCurrentWeatherAsync(port);

        Assert.Equal(port.Id, reading.PortId);
        Assert.Equal(10.8m, reading.WindSpeedMs);
        Assert.Equal(6, reading.BeaufortNumber);
        Assert.Equal(95, reading.WindDirectionDeg);
        Assert.Equal(12.25m, reading.WindGustMs);
        Assert.Equal(0m, reading.Rainfall1hMm);
        Assert.Equal(8m, reading.VisibilityKm);
        Assert.Equal(29.51m, reading.TemperatureC);
        Assert.Equal(78, reading.HumidityPct);
        Assert.Equal(1008m, reading.PressureHpa);
        Assert.Equal(801, reading.OpenWeatherCode);
        Assert.Equal("few clouds", reading.OpenWeatherDescription);
        Assert.Equal("02d", reading.OpenWeatherIcon);
        Assert.Equal("OPENWEATHER_API", reading.DataSource);
        Assert.Equal(responseBody, reading.RawPayload);
    }

    private static OpenWeatherService CreateService(string responseBody)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenWeather:ApiKey"] = "test-key"
            })
            .Build();

        var httpClient = new HttpClient(new StaticResponseHandler(responseBody))
        {
            BaseAddress = new Uri("https://openweather.test/data/2.5/")
        };

        return new OpenWeatherService(
            new StaticHttpClientFactory(httpClient),
            configuration,
            NullLogger<OpenWeatherService>.Instance);
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StaticHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StaticResponseHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
