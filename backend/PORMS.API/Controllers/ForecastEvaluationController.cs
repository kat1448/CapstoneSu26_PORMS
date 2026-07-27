using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/forecast-evaluation")]
public sealed class ForecastEvaluationController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ForecastEvaluationResponse>> GetEvaluation(
        [FromServices] ForecastEvaluationRepository repository,
        [FromQuery] string? portCode,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var rows = await repository.GetRowsAsync(portCode, from, to, cancellationToken);
        return Ok(ToResponse(rows));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromServices] ForecastEvaluationRepository repository,
        [FromQuery] string? portCode,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var rows = await repository.GetRowsAsync(portCode, from, to, cancellationToken);
        var csv = BuildCsv(rows);
        var fileName = $"forecast-evaluation-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", fileName);
    }

    private static ForecastEvaluationResponse ToResponse(IReadOnlyList<ForecastEvaluationRowReadModel> rows)
    {
        var matchedRows = rows.Where(item => item.ActualObservedAt is not null).ToList();
        var summary = new ForecastEvaluationSummaryResponse
        {
            TotalForecastPoints = rows.Count,
            MatchedActualPoints = matchedRows.Count,
            MatchRatePct = rows.Count == 0 ? 0 : Math.Round(matchedRows.Count * 100m / rows.Count, 1),
            AvgWindMae = Average(matchedRows.Select(item => item.WindAbsError)),
            AvgRainMae = Average(matchedRows.Select(item => item.RainAbsError)),
            AvgVisibilityMae = Average(matchedRows.Select(item => item.VisibilityAbsError)),
            AvgRiskScoreError = Average(matchedRows.Select(item => item.RiskScoreError.HasValue ? (decimal?)item.RiskScoreError.Value : null))
        };

        return new ForecastEvaluationResponse
        {
            Summary = summary,
            Rows = rows.Select(item => new ForecastEvaluationRowResponse
            {
                DatasetName = item.DatasetName,
                PortCode = item.PortCode,
                PortName = item.PortName,
                SnapshotNumber = item.SnapshotNumber,
                PlannedAt = item.PlannedAt,
                ActualObservedAt = item.ActualObservedAt,
                ForecastWindSpeedMs = item.ForecastWindSpeedMs,
                ActualWindSpeedMs = item.ActualWindSpeedMs,
                WindAbsError = item.WindAbsError,
                ForecastRainfallMm = item.ForecastRainfallMm,
                ActualRainfallMm = item.ActualRainfallMm,
                RainAbsError = item.RainAbsError,
                ForecastVisibilityKm = item.ForecastVisibilityKm,
                ActualVisibilityKm = item.ActualVisibilityKm,
                VisibilityAbsError = item.VisibilityAbsError,
                ForecastRiskLevel = item.ForecastRiskLevel,
                ActualRiskLevel = item.ActualRiskLevel,
                RiskScoreError = item.RiskScoreError,
                ActualDataSource = item.ActualDataSource,
                Status = item.Status
            }).ToList()
        };
    }

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var concreteValues = values.Where(item => item.HasValue).Select(item => item!.Value).ToList();
        return concreteValues.Count == 0 ? null : Math.Round(concreteValues.Average(), 2);
    }

    private static string BuildCsv(IReadOnlyList<ForecastEvaluationRowReadModel> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Dataset,Cang,Ten cang,Ngay du bao,Ngay thuc te,Gio du bao,Gio thuc te,Sai so gio,Mua du bao,Mua thuc te,Sai so mua,Tam nhin du bao,Tam nhin thuc te,Sai so tam nhin,Rui ro du bao,Rui ro thuc te,Sai so risk,Nguon doi chieu,Trang thai");

        foreach (var row in rows)
        {
            var values = new[]
            {
                row.DatasetName,
                row.PortCode,
                row.PortName,
                FormatDate(row.PlannedAt),
                row.ActualObservedAt.HasValue ? FormatDate(row.ActualObservedAt.Value) : "",
                FormatDecimal(row.ForecastWindSpeedMs),
                FormatNullableDecimal(row.ActualWindSpeedMs),
                FormatNullableDecimal(row.WindAbsError),
                FormatDecimal(row.ForecastRainfallMm),
                FormatNullableDecimal(row.ActualRainfallMm),
                FormatNullableDecimal(row.RainAbsError),
                FormatNullableDecimal(row.ForecastVisibilityKm),
                FormatNullableDecimal(row.ActualVisibilityKm),
                FormatNullableDecimal(row.VisibilityAbsError),
                row.ForecastRiskLevel,
                row.ActualRiskLevel ?? "",
                row.RiskScoreError?.ToString(CultureInfo.InvariantCulture) ?? "",
                row.ActualDataSource ?? "",
                row.Status
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string FormatDate(DateTimeOffset value) => value.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatNullableDecimal(decimal? value) => value.HasValue ? FormatDecimal(value.Value) : "";

    private static string EscapeCsv(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
