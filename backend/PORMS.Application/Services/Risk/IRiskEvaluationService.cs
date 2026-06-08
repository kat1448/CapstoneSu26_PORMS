using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public interface IRiskEvaluationService
{
    Task<RiskAssessment> EvaluateAsync(
        WeatherReading weatherReading,
        CancellationToken cancellationToken = default);

    Task<RiskAssessment> EvaluateAsync(
        Guid weatherReadingId,
        CancellationToken cancellationToken = default);
}
