using System.Globalization;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOrPortManager")]
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
        var rows = await repository.GetRowsAsync(portCode, from, to, ScopedPortId(), cancellationToken);
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
        var rows = await repository.GetRowsAsync(portCode, from, to, ScopedPortId(), cancellationToken);
        var csv = BuildCsv(rows);
        var fileName = $"forecast-evaluation-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", fileName);
    }

    // Tạo dữ liệu trình diễn trong bộ nhớ; không ghi vào database
    [HttpGet("demo")]
    public ActionResult<ForecastEvaluationResponse> GetInterventionDemo()
    {
        var response = ToResponse(BuildInterventionDemoRows());
        return Ok(new ForecastEvaluationResponse
        {
            Summary = response.Summary,
            Rows = response.Rows,
            IsDemonstration = true,
            DataNotice = "DỮ LIỆU MÔ PHỎNG - chỉ dùng để trình diễn quy trình kiểm chứng và can thiệp; không lưu database, không tính vào độ tin cậy thực tế."
        });
    }

    private static ForecastEvaluationResponse ToResponse(IReadOnlyList<ForecastEvaluationRowReadModel> rows)
    {
        var summary = ForecastConfidenceCalculator.Calculate(rows);

        return new ForecastEvaluationResponse
        {
            Summary = summary,
            IsDemonstration = false,
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

    private static IReadOnlyList<ForecastEvaluationRowReadModel> BuildInterventionDemoRows()
    {
        // Giữ thời điểm mô phỏng ở UTC để kết quả nhất quán giữa Windows và Docker.
        var today = new DateTimeOffset(
            DateTime.UtcNow.Date,
            TimeSpan.Zero);
        return new[]
        {
            DemoRow(1, today.AddDays(-5), 6, 6.5m, 0, 0, 10, 10, "LOW", "LOW"),
            DemoRow(2, today.AddDays(-4), 10, 10.5m, 8, 9, 7, 6.5m, "MEDIUM", "MEDIUM"),
            DemoRow(3, today.AddDays(-3), 7, 18, 1, 35, 10, 3, "LOW", "HIGH"),
            DemoRow(4, today.AddDays(-2), 11, 20, 8, 42, 7, 2.5m, "MEDIUM", "HIGH"),
            DemoRow(5, today.AddDays(-1), 16, 27, 18, 75, 5, 1, "HIGH", "CRITICAL")
        };
    }

    private static ForecastEvaluationRowReadModel DemoRow(
        int snapshot,
        DateTimeOffset plannedAt,
        decimal forecastWind,
        decimal actualWind,
        decimal forecastRain,
        decimal actualRain,
        decimal forecastVisibility,
        decimal actualVisibility,
        string forecastRisk,
        string actualRisk)
    {
        var riskError = Math.Abs(RiskScore(actualRisk) - RiskScore(forecastRisk));
        return new ForecastEvaluationRowReadModel(
            "Kịch bản kiểm chứng sai liên tiếp",
            "DNTSA-DEMO",
            "Cảng Tiên Sa (mô phỏng)",
            snapshot,
            plannedAt,
            plannedAt.AddMinutes(10),
            forecastWind,
            actualWind,
            Math.Abs(actualWind - forecastWind),
            forecastRain,
            actualRain,
            Math.Abs(actualRain - forecastRain),
            forecastVisibility,
            actualVisibility,
            Math.Abs(actualVisibility - forecastVisibility),
            forecastRisk,
            actualRisk,
            riskError,
            "FORECAST_VERIFICATION_DEMO",
            "MATCHED");
    }

    private static int RiskScore(string riskLevel) => riskLevel switch
    {
        "LOW" => 1,
        "MEDIUM" => 2,
        "HIGH" => 3,
        "CRITICAL" => 4,
        _ => 0
    };

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

    private Guid? ScopedPortId()
    {
        if (User.IsInRole("ADMIN")) return null;
        return Guid.TryParse(User.FindFirstValue("port_id"), out var portId) ? portId : Guid.Empty;
    }
}
