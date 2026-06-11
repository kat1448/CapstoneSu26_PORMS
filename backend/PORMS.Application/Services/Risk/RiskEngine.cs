using PORMS.Domain.Entities;

namespace PORMS.Application.Services.Risk;

public sealed class RiskEngine : IRiskEngine
{
    private readonly IRiskEvaluationService _riskEvaluationService;

    public RiskEngine(IRiskEvaluationService riskEvaluationService)
    {
        _riskEvaluationService = riskEvaluationService;
    }

    public Task<RiskAssessment> EvaluateRiskAsync(
        WeatherReading weatherReading,
        CancellationToken cancellationToken = default)
        => _riskEvaluationService.EvaluateAsync(weatherReading, cancellationToken);
}
