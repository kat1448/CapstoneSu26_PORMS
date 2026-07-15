using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/ml")]
public sealed class MlController : ControllerBase
{
    [HttpPost("forecast-risk-analysis")]
    public async Task<ActionResult<ForecastRiskAnalysisResponse>> AnalyzeForecastRisk(
        [FromBody] ForecastRiskAnalysisRequest request,
        [FromServices] ForecastRiskMlService service,
        [FromServices] OperationPlanLlmService planService,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new ErrorResponse { Error = "At least one forecast item is required." });
        }

        var analysis = service.Analyze(request);
        var plan = await planService.AnalyzeAsync(request, analysis, cancellationToken);
        return Ok(new ForecastRiskAnalysisResponse
        {
            PortCode = analysis.PortCode,
            ModelVersion = analysis.ModelVersion,
            Items = analysis.Items,
            LlmPlanAnalysis = plan
        });
    }
}
