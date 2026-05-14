using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Weather;

public interface IWeatherService
{
    Task<WeatherReading> FetchCurrentWeatherAsync(Port port, CancellationToken cancellationToken = default);
}
