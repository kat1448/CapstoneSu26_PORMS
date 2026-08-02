using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using PORMS.API.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace PORMS.Tests.Unit
{
    public sealed class RiskThresholdExcelServiceTests
    {
        private static readonly string[] Headers =
        [
            "Factor",
        "RiskLevel",
        "ComparisonOperator",
        "ThresholdValue",
        "Unit",
        "Description",
        "IsEnabled"
        ];

        private readonly RiskThresholdExcelService _excelService = new();
        private readonly RiskThresholdValidator _validator = new();

        [Fact]
        public async Task CreateTemplate_ThenParse_ReturnsValidConfiguration()
        {
            var template = _excelService.CreateTemplate(
                CreateValidThresholds());

            Assert.NotEmpty(template);

            using (var stream = new MemoryStream(template))
            using (var workbook = new XLWorkbook(stream))
            {
                var worksheet = Assert.Single(workbook.Worksheets);

                Assert.Equal(
                    RiskThresholdExcelService.WorksheetName,
                    worksheet.Name);

                Assert.Equal("Factor", worksheet.Cell(1, 1).GetString());
                Assert.Equal("WIND", worksheet.Cell(2, 1).GetString());
                Assert.Equal(">=", worksheet.Cell(2, 3).GetString());
            }

            var parseResult = await _excelService.ParseAsync(
                CreateFormFile(template, "risk-thresholds.xlsx"),
                CancellationToken.None);

            Assert.True(
                parseResult.IsValid,
                string.Join(Environment.NewLine,
                    parseResult.Errors.Select(error => error.Message)));

            Assert.Equal(12, parseResult.TotalRows);
            Assert.Equal(12, parseResult.Candidates.Count);

            var rowValidation =
                _validator.ValidateRows(parseResult.Candidates);

            Assert.True(rowValidation.IsValid);
            Assert.Empty(
                _validator.ValidateConfiguration(rowValidation.ValidRows));
        }

        [Fact]
        public async Task ParseAsync_WithWrongHeader_ReturnsHeaderError()
        {
            var fileContent = CreateWorkbook(
                RiskThresholdExcelService.WorksheetName,
                worksheet =>
                {
                    WriteHeaders(worksheet);
                    worksheet.Cell(1, 1).Value = "WrongFactorHeader";
                    worksheet.Cell(2, 1).Value = "WIND";
                });

            var result = await _excelService.ParseAsync(
                CreateFormFile(fileContent, "wrong-header.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error =>
                error.RowNumber == 1 &&
                error.Column == "Factor");
        }

        [Fact]
        public async Task ParseAsync_WithWrongWorksheetName_ReturnsError()
        {
            var fileContent = CreateWorkbook(
                "Sheet1",
                WriteHeaders);

            var result = await _excelService.ParseAsync(
                CreateFormFile(fileContent, "wrong-sheet.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error =>
                error.Column == "Worksheet" &&
                error.Message.Contains(
                    RiskThresholdExcelService.WorksheetName));
        }

        [Fact]
        public async Task ParseAsync_WithFormula_ReturnsError()
        {
            var template = _excelService.CreateTemplate(
                CreateValidThresholds());

            var fileWithFormula = EditWorkbook(
                template,
                worksheet =>
                {
                    // Công thức trả về WIND nhưng vẫn phải bị từ chối.
                    worksheet.Cell(2, 1).FormulaA1 = "\"WIND\"";
                });

            var result = await _excelService.ParseAsync(
                CreateFormFile(fileWithFormula, "formula.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error =>
                error.RowNumber == 2 &&
                error.Column == "Factor" &&
                error.Message.Contains("công thức Excel"));
        }

        [Fact]
        public async Task ParseAsync_WithOversizedFile_ReturnsError()
        {
            var oversizedContent = new byte[
                RiskThresholdExcelService.MaximumFileBytes + 1];

            var result = await _excelService.ParseAsync(
                CreateFormFile(oversizedContent, "too-large.xlsx"),
                CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error =>
                error.Column == "File" &&
                error.Message.Contains("1 MB"));
        }

        private static IReadOnlyList<ValidatedRiskThreshold>
            CreateValidThresholds()
        {
            return
            [
                New("WIND", "LOW", "GTE", 0, "Beaufort"),
            New("WIND", "MEDIUM", "GTE", 6, "Beaufort"),
            New("WIND", "HIGH", "GTE", 8, "Beaufort"),
            New("WIND", "CRITICAL", "GTE", 10, "Beaufort"),

            New("RAIN", "LOW", "GTE", 0, "mm/h"),
            New("RAIN", "MEDIUM", "GTE", 10, "mm/h"),
            New("RAIN", "HIGH", "GTE", 25, "mm/h"),
            New("RAIN", "CRITICAL", "GTE", 50, "mm/h"),

            New("VISIBILITY", "LOW", "GTE", 10, "km"),
            New("VISIBILITY", "MEDIUM", "LTE", 10, "km"),
            New("VISIBILITY", "HIGH", "LTE", 5, "km"),
            New("VISIBILITY", "CRITICAL", "LTE", 1, "km")
            ];

            static ValidatedRiskThreshold New(
                string factor,
                string riskLevel,
                string comparisonOperator,
                decimal thresholdValue,
                string unit)
            {
                return new ValidatedRiskThreshold(
                    0,
                    factor,
                    riskLevel,
                    comparisonOperator,
                    thresholdValue,
                    unit,
                    null,
                    true,
                    1);
            }
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
                // FormFile tạo thủ công không tự khởi tạo Headers.
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
                RiskThresholdExcelService.WorksheetName));

            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        }

        private static void WriteHeaders(IXLWorksheet worksheet)
        {
            for (var column = 1; column <= Headers.Length; column++)
            {
                worksheet.Cell(1, column).Value = Headers[column - 1];
            }
        }
    }
}
