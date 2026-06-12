namespace PORMS.Application.DTOs.Weather;

public sealed record WeatherFetchNowResponse(
    FetchJobDto FetchJob,
    WeatherReadingDto? WeatherReading);
