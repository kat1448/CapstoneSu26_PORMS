using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PORMS.API.Configuration;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed class OpenWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly OpenWeatherOptions _options;
    private readonly WeatherRepository _weatherRepository;

    public OpenWeatherService(
        HttpClient httpClient,
        IOptions<OpenWeatherOptions> options,
        WeatherRepository weatherRepository)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _weatherRepository = weatherRepository;
    }

    public async Task<OpenWeatherRefreshResult> RefreshActivePortsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenWeather API key is not configured.");
        }

        var ports = await _weatherRepository.GetActiveOpenWeatherPortsAsync(cancellationToken);
        var fetched = 0;

        foreach (var port in ports)
        {
            var reading = await FetchCurrentAsync(port, cancellationToken);
            await _weatherRepository.UpsertOpenWeatherReadingAsync(reading, cancellationToken);
            fetched++;
        }

        return new OpenWeatherRefreshResult(fetched);
    }

    private async Task<OpenWeatherReadingInput> FetchCurrentAsync(OpenWeatherPortReadModel port, CancellationToken cancellationToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var latitude = port.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = port.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"{baseUrl}/weather?lat={latitude}&lon={longitude}&appid={_options.ApiKey}&units=metric";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawPayload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;

        var wind = root.GetProperty("wind");
        var main = root.GetProperty("main");
        var observedAt = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("dt").GetInt64());
        var rain1h = TryGetNestedDecimal(root, "rain", "1h") ?? 0m;
        var visibilityKm = root.TryGetProperty("visibility", out var visibility)
            ? Math.Round(visibility.GetDecimal() / 1000m, 2)
            : 0m;
        var weatherItem = root.TryGetProperty("weather", out var weatherArray) && weatherArray.GetArrayLength() > 0
            ? weatherArray[0]
            : default;
        var weatherCode = weatherItem.ValueKind == JsonValueKind.Object && weatherItem.TryGetProperty("id", out var code)
            ? code.GetInt32()
            : (int?)null;
        var description = weatherItem.ValueKind == JsonValueKind.Object && weatherItem.TryGetProperty("description", out var desc)
            ? desc.GetString()
            : null;
        var windSpeed = wind.GetProperty("speed").GetDecimal();

        return new OpenWeatherReadingInput(
            port.PortId,
            windSpeed,
            ToBeaufort(windSpeed),
            wind.TryGetProperty("deg", out var deg) ? (short?)deg.GetInt16() : null,
            wind.TryGetProperty("gust", out var gust) ? gust.GetDecimal() : null,
            rain1h,
            main.GetProperty("temp").GetDecimal(),
            main.TryGetProperty("humidity", out var humidity) ? (short)humidity.GetInt16() : (short)0,
            visibilityKm,
            main.TryGetProperty("pressure", out var pressure) ? pressure.GetDecimal() : null,
            weatherCode,
            description,
            observedAt,
            $"OPENWEATHER:{port.PortId}:{observedAt.ToUnixTimeSeconds()}",
            rawPayload);
    }

    private static decimal? TryGetNestedDecimal(JsonElement root, string property, string nestedProperty)
    {
        return root.TryGetProperty(property, out var outer) && outer.TryGetProperty(nestedProperty, out var inner)
            ? inner.GetDecimal()
            : null;
    }

    private static short ToBeaufort(decimal windSpeedMs)
    {
        return windSpeedMs switch
        {
            < 0.3m => 0,
            < 1.6m => 1,
            < 3.4m => 2,
            < 5.5m => 3,
            < 8.0m => 4,
            < 10.8m => 5,
            < 13.9m => 6,
            < 17.2m => 7,
            < 20.8m => 8,
            < 24.5m => 9,
            < 28.5m => 10,
            < 32.7m => 11,
            _ => 12
        };
    }
}

public sealed record OpenWeatherRefreshResult(int FetchedCount);
