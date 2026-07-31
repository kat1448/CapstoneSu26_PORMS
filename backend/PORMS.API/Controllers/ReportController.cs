using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.Infrastructure.Repositories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOrPortManager")]
[Route("api/reports")]
public sealed class ReportController : ControllerBase
{
    private static readonly string[] Types = ["ALERTS", "TASKS", "EVENTS"];

    [HttpGet("preview")]
    public async Task<ActionResult<ReportPreviewResponse>> Preview(
        [FromQuery] string type, [FromQuery] string? portCode, [FromQuery] string? zoneName,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? riskLevel,
        [FromServices] ReportRepository repository, CancellationToken cancellationToken)
    {
        var request = ParseRequest(type, portCode, zoneName, from, to, riskLevel);
        if (request is null) return BadRequest(new { error = "Loại báo cáo hoặc khoảng thời gian không hợp lệ." });
        var access = GetAccess(User);
        var rows = await repository.GetRowsAsync(request.Type, request.PortCode, request.ZoneName, request.From, request.To, request.RiskLevel, access.UserId, access.PortId, access.Role, cancellationToken);
        return Ok(new ReportPreviewResponse { ReportType = request.Type, TotalRows = rows.Count, Rows = rows.Select(ToResponse).ToList() });
    }

    [HttpGet("export/{format}")]
    public async Task<IActionResult> Export(
        string format, [FromQuery] string type, [FromQuery] string? portCode, [FromQuery] string? zoneName,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? riskLevel,
        [FromServices] ReportRepository repository, [FromServices] OperationEventRepository eventRepository,
        CancellationToken cancellationToken)
    {
        var request = ParseRequest(type, portCode, zoneName, from, to, riskLevel);
        var normalizedFormat = format.Trim().ToUpperInvariant();
        if (request is null || normalizedFormat is not ("XLSX" or "PDF"))
            return BadRequest(new { error = "Định dạng hoặc bộ lọc báo cáo không hợp lệ." });
        var access = GetAccess(User);
        var rows = await repository.GetRowsAsync(request.Type, request.PortCode, request.ZoneName, request.From, request.To, request.RiskLevel, access.UserId, access.PortId, access.Role, cancellationToken);
        if (rows.Count == 0) return NotFound(new { error = "Không có dữ liệu phù hợp để tạo báo cáo." });
        var title = ReportTitle(request.Type);
        var fileStem = $"PORMS_{request.Type}_{request.PortCode ?? "TOAN_CANG"}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        byte[] content;
        string contentType;
        string extension;
        if (normalizedFormat == "XLSX") { content = BuildExcel(title, rows); contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; extension = "xlsx"; }
        else { content = BuildPdf(title, rows); contentType = "application/pdf"; extension = "pdf"; }
        await eventRepository.RecordReportExportAsync(access.UserId, access.PortId, request.Type, normalizedFormat, BuildFilterSummary(request), cancellationToken);
        return File(content, contentType, $"{fileStem}.{extension}");
    }

    private static byte[] BuildExcel(string title, IReadOnlyList<ReportRowReadModel> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bao cao");
        sheet.Cell(1, 1).Value = title; sheet.Range(1, 1, 1, 9).Merge();
        sheet.Cell(2, 1).Value = "Tạo lúc"; sheet.Cell(2, 2).Value = DateTimeOffset.UtcNow.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var headers = new[] { "Thời gian", "Mã cảng", "Tên cảng", "Khu vực", "Mức độ", "Nội dung", "Chi tiết", "Người phụ trách/nguồn", "Trạng thái" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(4, i + 1).Value = headers[i];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; var values = new object?[] { row.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), row.PortCode, row.PortName, row.ZoneName, RiskLabel(row.RiskLevel), SubjectLabel(row.Subject), row.Description, row.Owner, StatusLabel(row.Status) };
            for (var j = 0; j < values.Length; j++) sheet.Cell(i + 5, j + 1).Value = values[j]?.ToString() ?? "-";
        }
        var table = sheet.Range(4, 1, rows.Count + 4, headers.Length).CreateTable(); table.Theme = XLTableTheme.TableStyleMedium2;
        sheet.Row(1).Style.Font.Bold = true; sheet.Row(1).Style.Font.FontSize = 16; sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private static byte[] BuildPdf(string title, IReadOnlyList<ReportRowReadModel> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Margin(28); page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Text(title).FontSize(18).Bold().FontColor(Colors.Blue.Medium);
            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns => { for (var i = 0; i < 6; i++) columns.RelativeColumn(); });
                foreach (var header in new[] { "Thời gian", "Cảng", "Khu vực", "Mức độ", "Nội dung", "Trạng thái" }) table.Cell().Background(Colors.Blue.Medium).Padding(4).Text(header).FontColor(Colors.White).Bold();
                foreach (var row in rows) { table.Cell().Padding(4).Text(row.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")); table.Cell().Padding(4).Text($"{row.PortCode} - {row.PortName}"); table.Cell().Padding(4).Text(row.ZoneName); table.Cell().Padding(4).Text(RiskLabel(row.RiskLevel)); table.Cell().Padding(4).Text(SubjectLabel(row.Subject)); table.Cell().Padding(4).Text(StatusLabel(row.Status)); }
            });
            page.Footer().AlignCenter().Text($"PORMS · Tạo lúc {DateTimeOffset.Now:dd/MM/yyyy HH:mm}");
        })).GeneratePdf();
    }

    private static ReportPreviewRowResponse ToResponse(ReportRowReadModel row) => new() { OccurredAt = row.OccurredAt, PortCode = row.PortCode, PortName = row.PortName, ZoneName = row.ZoneName, RiskLevel = row.RiskLevel, Subject = SubjectLabel(row.Subject), Description = row.Description, Owner = row.Owner, Status = StatusLabel(row.Status) };
    private static string RiskLabel(string? value) => value switch { "LOW" => "Thấp", "MEDIUM" => "Cần lưu ý", "HIGH" => "Cao", "CRITICAL" => "Rất cao", _ => "-" };
    private static string StatusLabel(string value) => value switch { "NEW" => "Chờ tiếp nhận", "ACKNOWLEDGED" => "Đã tiếp nhận", "IN_PROGRESS" => "Đang thực hiện", "COMPLETED" => "Đã hoàn tất", "CANCELLED" => "Đã hủy", _ => value };
    private static string SubjectLabel(string value) => value switch { "WEATHER_FETCHED" => "Đã cập nhật dữ liệu thời tiết", "RISK_ASSESSED" or "RISK_EVALUATED" => "Đã đánh giá mức độ rủi ro", "ALERT_CREATED" => "Đã phát cảnh báo", "TASK_CREATED" => "Đã tạo nhiệm vụ", "TASK_ASSIGNED" => "Đã phân công nhiệm vụ", "TASK_ACKNOWLEDGED" => "Đã tiếp nhận nhiệm vụ", "TASK_STARTED" => "Đã bắt đầu nhiệm vụ", "TASK_COMPLETED" => "Đã hoàn tất nhiệm vụ", "REPORT_EXPORTED" => "Đã xuất báo cáo", "SIMULATION_STARTED" => "Bắt đầu mô phỏng", "SIMULATION_STEP" => "Cập nhật tình huống mô phỏng", "SIMULATION_COMPLETED" => "Hoàn tất mô phỏng", _ => value };
    private static string ReportTitle(string type) => type switch { "ALERTS" => "Báo cáo cảnh báo PORMS", "TASKS" => "Báo cáo nhiệm vụ PORMS", _ => "Báo cáo nhật ký vận hành PORMS" };
    private static string BuildFilterSummary(ReportRequest r) => $"cảng={r.PortCode ?? "tất cả"}; khu vực={r.ZoneName ?? "tất cả"}; từ={r.From?.ToString("O") ?? "-"}; đến={r.To?.ToString("O") ?? "-"}; mức={r.RiskLevel ?? "tất cả"}";
    private static ReportRequest? ParseRequest(string type, string? portCode, string? zoneName, DateTimeOffset? from, DateTimeOffset? to, string? riskLevel) => Types.Contains(type.Trim().ToUpperInvariant()) && (!from.HasValue || !to.HasValue || from <= to) ? new(type.Trim().ToUpperInvariant(), portCode?.Trim().ToUpperInvariant(), zoneName?.Trim(), from, to, riskLevel?.Trim().ToUpperInvariant()) : null;
    private static (Guid UserId, Guid? PortId, string Role) GetAccess(ClaimsPrincipal user) => (Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!), Guid.TryParse(user.FindFirstValue("port_id"), out var portId) ? portId : null, user.FindFirstValue(ClaimTypes.Role) ?? string.Empty);
}

public sealed record ReportRequest(string Type, string? PortCode, string? ZoneName, DateTimeOffset? From, DateTimeOffset? To, string? RiskLevel);
public sealed class ReportPreviewResponse { public required string ReportType { get; init; } public int TotalRows { get; init; } public required IReadOnlyList<ReportPreviewRowResponse> Rows { get; init; } }
public sealed class ReportPreviewRowResponse { public DateTimeOffset OccurredAt { get; init; } public required string PortCode { get; init; } public required string PortName { get; init; } public required string ZoneName { get; init; } public string? RiskLevel { get; init; } public required string Subject { get; init; } public required string Description { get; init; } public required string Owner { get; init; } public required string Status { get; init; } }
