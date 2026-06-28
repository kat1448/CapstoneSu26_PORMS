namespace PORMS.API.Configuration;

public sealed class OpenWeatherOptions
{
    public const string SectionName = "OpenWeather";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5";
}
