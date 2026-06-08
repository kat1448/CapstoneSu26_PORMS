using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PORMS.Application.Services.Weather;
using PORMS.Domain.Entities;

namespace PORMS.Infrastructure.Weather;

public sealed class OpenWeatherService : IWeatherService
{
    private const string ClientName = "OpenWeather";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenWeatherService> _logger;

    public OpenWeatherService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenWeatherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WeatherReading> FetchCurrentWeatherAsync(
        Port port,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetRequiredApiKey();
        var client = _httpClientFactory.CreateClient(ClientName);

        var query = BuildCurrentWeatherQuery(port, apiKey);
        var stopwatch = Stopwatch.StartNew();

        var (response, responseBody) = await GetWithRetriesAsync(
            client,
            query,
            port,
            stopwatch,
            cancellationToken);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenWeather request failed for port {PortId} ({PortCode}). Status={StatusCode}. Body={Body}",
                    port.Id,
                    port.Code,
                    response.StatusCode,
                    Truncate(responseBody, 512));

                throw new OpenWeatherException(
                    $"OpenWeather request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                    response.StatusCode,
                    responseBody);
            }
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var windSpeedMs = GetDecimal(root, "wind", "speed") ?? 0m;
            var observedAt = GetUnixTimestamp(root, "dt") ?? DateTimeOffset.UtcNow;
            var weather = root.TryGetProperty("weather", out var weatherArray) &&
                          weatherArray.ValueKind == JsonValueKind.Array &&
                          weatherArray.GetArrayLength() > 0
                ? weatherArray[0]
                : default;

            return new WeatherReading
            {
                Id = Guid.NewGuid(),
                PortId = port.Id,
                WindSpeedMs = Round(windSpeedMs, 2),
                BeaufortNumber = ConvertToBeaufort(windSpeedMs),
                WindDirectionDeg = GetInt(root, "wind", "deg"),
                WindGustMs = RoundNullable(GetDecimal(root, "wind", "gust"), 2),
                Rainfall1hMm = RoundNullable(GetDecimal(root, "rain", "1h") ?? 0m, 2),
                Rainfall3hMm = RoundNullable(GetDecimal(root, "rain", "3h"), 2),
                TemperatureC = RoundNullable(GetDecimal(root, "main", "temp"), 2),
                HumidityPct = GetInt(root, "main", "humidity"),
                VisibilityKm = RoundNullable(MetersToKilometers(GetDecimal(root, "visibility")), 2),
                PressureHpa = RoundNullable(GetDecimal(root, "main", "pressure"), 2),
                OpenWeatherCode = weather.ValueKind == JsonValueKind.Object ? GetInt(weather, "id") : null,
                OpenWeatherDescription = weather.ValueKind == JsonValueKind.Object ? GetString(weather, "description") : null,
                OpenWeatherIcon = weather.ValueKind == JsonValueKind.Object ? GetString(weather, "icon") : null,
                ObservedAt = observedAt,
                RecordedAt = DateTimeOffset.UtcNow,
                DataSource = "OPENWEATHER_API",
                RawPayload = responseBody,
                IsSimulation = false
            };
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "OpenWeather returned invalid JSON for port {PortId} ({PortCode}). Body={Body}",
                port.Id,
                port.Code,
                Truncate(responseBody, 512));

            throw new OpenWeatherException("OpenWeather returned invalid JSON.", response.StatusCode, responseBody, exception);
        }
    }

    private async Task<(HttpResponseMessage Response, string ResponseBody)> GetWithRetriesAsync(
        HttpClient client,
        string query,
        Port port,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await client.GetAsync(query, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (IsRetryableServerError(response.StatusCode) && attempt < maxRetries)
                {
                    response.Dispose();
                    await DelayBeforeRetryAsync(attempt, port, response.StatusCode, cancellationToken);
                    continue;
                }

                stopwatch.Stop();
                return (response, responseBody);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException exception)
            {
                stopwatch.Stop();
                throw new OpenWeatherException(
                    $"OpenWeather request timed out after {stopwatch.ElapsedMilliseconds}ms.",
                    null,
                    null,
                    exception);
            }
            catch (HttpRequestException exception) when (attempt < maxRetries)
            {
                await DelayBeforeRetryAsync(attempt, port, exception.StatusCode, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                throw new OpenWeatherException(
                    "OpenWeather request failed before receiving a response.",
                    exception.StatusCode,
                    null,
                    exception);
            }
        }
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        Port port,
        HttpStatusCode? statusCode,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        _logger.LogWarning(
            "Retrying OpenWeather for port {PortCode} ({PortId}) in {DelaySeconds}s after attempt {Attempt}. StatusCode={StatusCode}.",
            port.Code,
            port.Id,
            delay.TotalSeconds,
            attempt + 1,
            statusCode);
        await Task.Delay(delay, cancellationToken);
    }

    private static bool IsRetryableServerError(HttpStatusCode statusCode)
        => (int)statusCode >= 500;

    public static int ConvertToBeaufort(decimal windSpeedMs)
    {
        if (windSpeedMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windSpeedMs), "Wind speed must be greater than or equal to 0.");
        }

        var rounded = Math.Round(windSpeedMs, 1, MidpointRounding.AwayFromZero);

        return rounded switch
        {
            <= 0.2m => 0,
            <= 1.5m => 1,
            <= 3.3m => 2,
            <= 5.4m => 3,
            <= 7.9m => 4,
            <= 10.7m => 5,
            <= 13.8m => 6,
            <= 17.1m => 7,
            <= 20.7m => 8,
            <= 24.4m => 9,
            <= 28.4m => 10,
            <= 32.6m => 11,
            _ => 12
        };
    }

    private string GetRequiredApiKey()
    {
        var apiKey = _configuration["OpenWeather:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenWeather API key is missing. Set OPENWEATHER_API_KEY in .env or OpenWeather:ApiKey in configuration.");
        }

        return apiKey;
    }

    private static string BuildCurrentWeatherQuery(Port port, string apiKey)
    {
        var lat = port.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = port.Longitude.ToString(CultureInfo.InvariantCulture);
        return $"weather?lat={lat}&lon={lon}&appid={Uri.EscapeDataString(apiKey)}&units=metric";
    }

    private static decimal? GetDecimal(JsonElement root, params string[] path)
    {
        if (!TryGet(root, out var value, path))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result)
            ? result
            : null;
    }

    private static int? GetInt(JsonElement root, params string[] path)
    {
        if (!TryGet(root, out var value, path))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static string? GetString(JsonElement root, params string[] path)
    {
        if (!TryGet(root, out var value, path))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static DateTimeOffset? GetUnixTimestamp(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    private static bool TryGet(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static decimal? MetersToKilometers(decimal? meters)
        => meters.HasValue ? meters.Value / 1000m : null;

    private static decimal Round(decimal value, int digits)
        => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static decimal? RoundNullable(decimal? value, int digits)
        => value.HasValue ? Round(value.Value, digits) : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class OpenWeatherException : Exception
{
    public OpenWeatherException(string message, HttpStatusCode? statusCode, string? responseBody, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }
}
