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

    public async Task<OpenWeatherForecastReadModel> FetchDailyForecastAsync(
        string portCode,
        int days,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenWeather API key is not configured.");
        }

        if (days is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Forecast days must be between 1 and 5.");
        }

        var port = await _weatherRepository.GetOpenWeatherPortAsync(portCode, cancellationToken)
            ?? throw new InvalidOperationException($"OpenWeather port {portCode.Trim().ToUpperInvariant()} was not found.");
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var latitude = port.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = port.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"{baseUrl}/forecast?lat={latitude}&lon={longitude}&appid={_options.ApiKey}&units=metric&lang=vi";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawPayload = await response.Content.ReadAsStringAsync(cancellationToken);
        var fetchedAt = DateTimeOffset.UtcNow;
        using var document = JsonDocument.Parse(rawPayload);
        var forecastDays = OpenWeatherForecastParser.ParseDailyForecast(document.RootElement, days, fetchedAt);

        return new OpenWeatherForecastReadModel(
            port.PortCode,
            port.PortName,
            fetchedAt,
            forecastDays);
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

public static class OpenWeatherForecastParser
{
    public static IReadOnlyList<OpenWeatherForecastDayReadModel> ParseDailyForecast(
        JsonElement root,
        int days,
        DateTimeOffset? fetchedAt = null)
    {
        if (!root.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var timezoneOffset = GetTimezoneOffset(root);
        var currentLocalDate = fetchedAt?.ToOffset(timezoneOffset).Date;
        var groups = list.EnumerateArray()
            .Select(ParseForecastItem)
            .GroupBy(item => item.Date.ToOffset(timezoneOffset).Date)
            .OrderBy(group => group.Key)
            .Where(group => currentLocalDate is null || group.Key > currentLocalDate.Value)
            .Take(days)
            .Select(ToDailyForecast)
            .ToList();

        return groups;
    }

    private static TimeSpan GetTimezoneOffset(JsonElement root)
    {
        if (!root.TryGetProperty("city", out var city)
            || !city.TryGetProperty("timezone", out var timezone)
            || !timezone.TryGetInt32(out var offsetSeconds))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(offsetSeconds);
    }

    private static ForecastItem ParseForecastItem(JsonElement item)
    {
        var main = item.GetProperty("main");
        var wind = item.TryGetProperty("wind", out var windElement) ? windElement : default;
        var weatherItem = item.TryGetProperty("weather", out var weatherArray) && weatherArray.ValueKind == JsonValueKind.Array && weatherArray.GetArrayLength() > 0
            ? weatherArray[0]
            : default;
        var rainMm = item.TryGetProperty("rain", out var rain) && rain.TryGetProperty("3h", out var rain3h)
            ? rain3h.GetDecimal()
            : 0m;

        return new ForecastItem(
            DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()),
            main.TryGetProperty("temp", out var temp) ? temp.GetDecimal() : 0m,
            main.TryGetProperty("temp_min", out var tempMin) ? tempMin.GetDecimal() : 0m,
            main.TryGetProperty("temp_max", out var tempMax) ? tempMax.GetDecimal() : 0m,
            item.TryGetProperty("visibility", out var visibility) ? Math.Round(visibility.GetDecimal() / 1000m, 2) : null,
            wind.ValueKind == JsonValueKind.Object && wind.TryGetProperty("speed", out var windSpeed) ? windSpeed.GetDecimal() : 0m,
            wind.ValueKind == JsonValueKind.Object && wind.TryGetProperty("gust", out var windGust) ? windGust.GetDecimal() : null,
            wind.ValueKind == JsonValueKind.Object && wind.TryGetProperty("deg", out var windDeg) ? (short?)windDeg.GetInt16() : null,
            rainMm,
            item.TryGetProperty("pop", out var pop) ? (short)Math.Round(pop.GetDecimal() * 100m) : (short)0,
            main.TryGetProperty("humidity", out var humidity) ? (short)humidity.GetInt16() : (short)0,
            main.TryGetProperty("pressure", out var pressure) ? pressure.GetDecimal() : null,
            weatherItem.ValueKind == JsonValueKind.Object && weatherItem.TryGetProperty("id", out var code) ? code.GetInt32() : null,
            weatherItem.ValueKind == JsonValueKind.Object && weatherItem.TryGetProperty("description", out var description) ? description.GetString() : null);
    }

    private static OpenWeatherForecastDayReadModel ToDailyForecast(IGrouping<DateTime, ForecastItem> group)
    {
        var items = group.ToList();
        var strongestWind = items.OrderByDescending(item => item.WindSpeedMs).First();
        var highestPop = items.OrderByDescending(item => item.PopPct).First();
        var representativeWeather = items
            .OrderByDescending(item => item.PopPct)
            .ThenByDescending(item => item.RainMm)
            .First();

        return new OpenWeatherForecastDayReadModel(
            new DateTimeOffset(group.Key, TimeSpan.Zero),
            Math.Round(items.Average(item => item.TemperatureC), 1),
            items.Min(item => item.TemperatureMinC),
            items.Max(item => item.TemperatureMaxC),
            items.Any(item => item.VisibilityKm.HasValue)
                ? items.Where(item => item.VisibilityKm.HasValue).Min(item => item.VisibilityKm!.Value)
                : null,
            strongestWind.WindSpeedMs,
            items.Where(item => item.WindGustMs.HasValue).Max(item => item.WindGustMs),
            strongestWind.WindDirectionDeg,
            items.Sum(item => item.RainMm),
            highestPop.PopPct,
            (short)Math.Round(items.Average(item => item.HumidityPct)),
            items.Any(item => item.PressureHpa.HasValue)
                ? Math.Round(items.Where(item => item.PressureHpa.HasValue).Average(item => item.PressureHpa!.Value), 0)
                : null,
            representativeWeather.WeatherCode,
            representativeWeather.WeatherDescription,
            representativeWeather.WeatherDescription);
    }

    private sealed record ForecastItem(
        DateTimeOffset Date,
        decimal TemperatureC,
        decimal TemperatureMinC,
        decimal TemperatureMaxC,
        decimal? VisibilityKm,
        decimal WindSpeedMs,
        decimal? WindGustMs,
        short? WindDirectionDeg,
        decimal RainMm,
        short PopPct,
        short HumidityPct,
        decimal? PressureHpa,
        int? WeatherCode,
        string? WeatherDescription);
}

public sealed record OpenWeatherForecastReadModel(
    string PortCode,
    string PortName,
    DateTimeOffset FetchedAt,
    IReadOnlyList<OpenWeatherForecastDayReadModel> Days);

public sealed record OpenWeatherForecastDayReadModel(
    DateTimeOffset Date,
    decimal TemperatureDayC,
    decimal TemperatureMinC,
    decimal TemperatureMaxC,
    decimal? VisibilityKm,
    decimal WindSpeedMs,
    decimal? WindGustMs,
    short? WindDirectionDeg,
    decimal RainMm,
    short PopPct,
    short HumidityPct,
    decimal? PressureHpa,
    int? WeatherCode,
    string? WeatherDescription,
    string? Summary);
