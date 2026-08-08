using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using PORMS.API.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PORMS.Tests.Unit
{
    public sealed class SopRuleImportTests
    {
        private static readonly string[] Headers =
        [
            "RuleCode",
            "RuleName",
            "Description",
            "TriggerRiskLevel",
            "PreviousRiskLevel",
            "AppliesToZoneType",
            "ActionType",
            "ActionConfigJson",
            "ExecutionOrder",
            "IsActive"
        ];

        private readonly SopRuleExcelService _excelService = new();
        private readonly SopRuleImportValidator _validator = new();

        [Fact]
        public async Task CreateTemplate_ThenParse_ReturnsValidSopRules()
        {
            var template = _excelService.CreateTemplate(CreateValidRules());

            Assert.NotEmpty(template);

            using (var stream = new MemoryStream(template))
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = Assert.Single(workbook.Worksheets);

                Assert.Equal(
                    SopRuleExcelService.WorksheetName,
                    worksheet.Name);

                Assert.Equal("RuleCode", worksheet.Cell(1, 1).GetString());

                // Template phải sắp xếp LOW trước HIGH và CRITICAL
                Assert.Equal(
                    "SOP-LOW-ALL-01",
                    worksheet.Cell(2, 1).GetString());

                Assert.Equal(
                    "ANY",
                    worksheet.Cell(2, 5).GetString());

                Assert.Equal(
                    "ALL",
                    worksheet.Cell(2, 6).GetString());

                Assert.True(worksheet.Column(8).IsHidden);
                Assert.True(worksheet.Column(9).IsHidden);
            }

            var parseResult = await _excelService.ParseAsync(
                CreateFormFile(template, "sop-rules.xlsx"),
                CancellationToken.None);

            Assert.True(
                parseResult.IsValid,
                string.Join(
                    Environment.NewLine,
                    parseResult.Errors.Select(error => error.Message)));

            Assert.Equal(3, parseResult.TotalRows);
            Assert.Equal(3, parseResult.Candidates.Count);

            var validation =
                _validator.ValidateRows(parseResult.Candidates);

            Assert.True(
                validation.IsValid,
                string.Join(
                    Environment.NewLine,
                    validation.Errors.Select(error => error.Message)));

            var highRule = Assert.Single(
                validation.ValidRows,
                rule => rule.RuleCode == "SOP-HIGH-DOCK-01");

            Assert.Equal("HIGH", highRule.TriggerRiskLevel);
            Assert.Equal("DOCK", highRule.AppliesToZoneType);
            Assert.Equal("SET_LIMITED_MODE", highRule.ActionType);
        }

        [Fact]
        public async Task ParseAsync_WithWrongHeader_ReturnsHeaderError()
        {
            var content = CreateWorkbook(
                SopRuleExcelService.WorksheetName,
                worksheet =>
                {
                    WriteHeaders(worksheet);
                    worksheet.Cell(1, 1).Value = "WrongRuleCode";
                });

            var result = await _excelService.ParseAsync(
                CreateFormFile(content, "wrong-header.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.RowNumber == 1 &&
                error.Column == "RuleCode");
        }

        [Fact]
        public async Task ParseAsync_WithWrongWorksheetName_ReturnsError()
        {
            var content = CreateWorkbook(
                "Sheet1",
                WriteHeaders);

            var result = await _excelService.ParseAsync(
                CreateFormFile(content, "wrong-sheet.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.Column == "Worksheet" &&
                error.Message.Contains(
                    SopRuleExcelService.WorksheetName));
        }

        [Fact]
        public async Task ParseAsync_WithFormula_ReturnsError()
        {
            var template = _excelService.CreateTemplate(CreateValidRules());

            var content = EditWorkbook(
                template,
                worksheet =>
                {
                    // Công thức trả về RuleCode nhưng vẫn phải bị từ chối
                    worksheet.Cell(2, 1).FormulaA1 =
                        "\"SOP-LOW-ALL-01\"";
                });

            var result = await _excelService.ParseAsync(
                CreateFormFile(content, "formula.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.RowNumber == 2 &&
                error.Column == "RuleCode" &&
                error.Message.Contains("công thức Excel"));
        }

        [Fact]
        public async Task ParseAsync_WithOversizedFile_ReturnsError()
        {
            var oversizedContent =
                new byte[SopRuleExcelService.MaximumFileBytes + 1];

            var result = await _excelService.ParseAsync(
                CreateFormFile(oversizedContent, "too-large.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.Column == "File" &&
                error.Message.Contains("1 MB"));
        }

        [Fact]
        public void ValidateRows_WithValidInput_NormalizesValues()
        {
            var rows = new[]
            {
                CreateCandidate(
                    rowNumber: 2,
                    ruleCode: " sop-critical-all-01 ",
                    ruleName: "  Dừng hoạt động  ",
                    riskLevel: " critical ",
                    zoneType: " all ",
                    actionType: " stop_operations ",
                    actionConfigJson:
                        "{\"mode\":\"stop\",\"sendAlert\":true}",
                    previousRiskLevel: " any ")
            };

            var result = _validator.ValidateRows(rows);

            Assert.True(result.IsValid);

            var rule = Assert.Single(result.ValidRows);

            Assert.Equal("SOP-CRITICAL-ALL-01", rule.RuleCode);
            Assert.Equal("Dừng hoạt động", rule.RuleName);
            Assert.Equal("CRITICAL", rule.TriggerRiskLevel);
            Assert.Null(rule.PreviousRiskLevel);
            Assert.Null(rule.AppliesToZoneType);
            Assert.Equal("STOP_OPERATIONS", rule.ActionType);
            Assert.Equal(20, rule.ExecutionOrder);
        }

        [Fact]
        public void ValidateRows_WithoutTechnicalFields_GeneratesDefaults()
        {
            var rows = new[]
            {
                CreateCandidate(
                    2,
                    "SOP-HIGH-TASK-01",
                    "Kiểm tra cầu cảng",
                    "HIGH",
                    "DOCK",
                    "CREATE_TASK",
                    null)
            };

            var result = _validator.ValidateRows(rows);

            Assert.True(result.IsValid);

            var rule = Assert.Single(result.ValidRows);
            Assert.Equal(30, rule.ExecutionOrder);

            using var document = JsonDocument.Parse(
                rule.ActionConfigJson);

            Assert.Equal(
                "Kiểm tra cầu cảng",
                document.RootElement.GetProperty("title").GetString());
            Assert.Equal(
                "HIGH",
                document.RootElement.GetProperty("priority").GetString());
        }

        [Fact]
        public void ValidateRows_WithDuplicateRuleCode_ReturnsError()
        {
            var rows = new[]
            {
                CreateCandidate(
                    2,
                    "SOP-HIGH-01",
                    "Quy tắc thứ nhất",
                    "HIGH",
                    "DOCK",
                    "SEND_ALERT",
                    "{}"),

                CreateCandidate(
                    3,
                    " sop-high-01 ",
                    "Quy tắc thứ hai",
                    "HIGH",
                    "YARD",
                    "SEND_ALERT",
                    "{}")
            };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);

            var duplicateError = Assert.Single(
                result.Errors,
                error => error.Column == "RuleCode");

            Assert.Equal(3, duplicateError.RowNumber);
            Assert.Contains("dòng 2", duplicateError.Message);
        }

        [Fact]
        public void ValidateRows_WithInvalidEnumsAndJson_ReturnsErrors()
        {
            var rows = new[]
            {
                CreateCandidate(
                    2,
                    "SOP-INVALID-01",
                    "Quy tắc không hợp lệ",
                    "DANGER",
                    "SEA",
                    "FLY_AWAY",
                    "[]")
            };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.Column == "TriggerRiskLevel");

            Assert.Contains(result.Errors, error =>
                error.Column == "AppliesToZoneType");

            Assert.Contains(result.Errors, error =>
                error.Column == "ActionType");

            Assert.Contains(result.Errors, error =>
                error.Column == "ActionConfigJson" &&
                error.Message.Contains("JSON object"));
        }

        [Fact]
        public void ValidateRows_WithMissingOrConflictingActionConfig_ReturnsErrors()
        {
            var rows = new[]
            {
                CreateCandidate(
                    2,
                    "SOP-TASK-01",
                    "Tạo nhiệm vụ",
                    "HIGH",
                    "YARD",
                    "CREATE_TASK",
                    "{}"),

                CreateCandidate(
                    3,
                    "SOP-LIMITED-01",
                    "Hạn chế vận hành",
                    "HIGH",
                    "DOCK",
                    "SET_LIMITED_MODE",
                    "{\"mode\":\"NORMAL\"}")
            };

            var result = _validator.ValidateRows(rows);

            Assert.False(result.IsValid);

            Assert.Contains(result.Errors, error =>
                error.RowNumber == 2 &&
                error.Message.Contains("title"));

            Assert.Contains(result.Errors, error =>
                error.RowNumber == 2 &&
                error.Message.Contains("priority"));

            Assert.Contains(result.Errors, error =>
                error.RowNumber == 3 &&
                error.Message.Contains("mode LIMITED"));
        }

        private static IReadOnlyList<ValidatedSopRuleImport>
            CreateValidRules()
        {
            // Cố ý không sắp xếp để kiểm tra thứ tự trong template
            return
            [
                new ValidatedSopRuleImport(
                    0,
                    "SOP-CRITICAL-ALL-01",
                    "Dừng toàn bộ hoạt động",
                    "Dừng vận hành khi rủi ro nghiêm trọng.",
                    "CRITICAL",
                    null,
                    null,
                    "STOP_OPERATIONS",
                    "{\"mode\":\"STOP\",\"sendAlert\":true}",
                    5,
                    true),

                new ValidatedSopRuleImport(
                    0,
                    "SOP-HIGH-DOCK-01",
                    "Hạn chế bốc xếp",
                    "Hạn chế hoạt động tại cầu cảng.",
                    "HIGH",
                    null,
                    "DOCK",
                    "SET_LIMITED_MODE",
                    "{\"mode\":\"LIMITED\",\"createTask\":true}",
                    10,
                    true),

                new ValidatedSopRuleImport(
                    0,
                    "SOP-LOW-ALL-01",
                    "Khôi phục vận hành",
                    "Khôi phục sau khi kiểm tra an toàn.",
                    "LOW",
                    null,
                    null,
                    "SET_NORMAL_MODE",
                    "{\"mode\":\"NORMAL\",\"requiresInspection\":true}",
                    100,
                    true)
            ];
        }

        private static SopRuleImportCandidate CreateCandidate(
            int rowNumber,
            string ruleCode,
            string ruleName,
            string riskLevel,
            string? zoneType,
            string actionType,
            string? actionConfigJson,
            string? previousRiskLevel = null)
        {
            return new SopRuleImportCandidate(
                RowNumber: rowNumber,
                RuleCode: ruleCode,
                RuleName: ruleName,
                Description: "Mô tả kiểm thử.",
                TriggerRiskLevel: riskLevel,
                PreviousRiskLevel: previousRiskLevel,
                AppliesToZoneType: zoneType,
                ActionType: actionType,
                ActionConfigJson: actionConfigJson,
                ExecutionOrder: null,
                IsActive: true);
        }

        private static IFormFile CreateFormFile(
            byte[] content,
            string fileName)
        {
            return new FormFile(
                new MemoryStream(content),
                0,
                content.Length,
                "file",
                fileName)
            {
                // FormFile thủ công cần Headers trước khi gán ContentType
                Headers = new HeaderDictionary(),
                ContentType =
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        private static byte[] CreateWorkbook(
            string worksheetName,
            Action<IXLWorksheet> configure)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(worksheetName);

            configure(worksheet);

            using var output = new MemoryStream();
            workbook.SaveAs(output);

            return output.ToArray();
        }

        private static byte[] EditWorkbook(
            byte[] source,
            Action<IXLWorksheet> edit)
        {
            using var input = new MemoryStream(source);
            using var workbook = new XLWorkbook(input);

            edit(workbook.Worksheet(
                SopRuleExcelService.WorksheetName));

            using var output = new MemoryStream();
            workbook.SaveAs(output);

            return output.ToArray();
        }

        private static void WriteHeaders(IXLWorksheet worksheet)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                worksheet.Cell(1, column).Value =
                    Headers[column - 1];
            }
        }
    }
}
