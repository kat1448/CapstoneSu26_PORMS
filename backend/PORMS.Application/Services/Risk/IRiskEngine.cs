using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public interface IRiskEngine
{
    Task<RiskAssessment> EvaluateRiskAsync(
        WeatherReading weatherReading,
        CancellationToken cancellationToken = default);
}
