using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace PORMS.API.Services
{
    /// Đọc file Excel cấu hình ngưỡng
    /// Service này chỉ phân tích file, hoàn toàn không ghi database
    public sealed class RiskThresholdExcelService
    {
        public const string WorksheetName = "RiskThresholds";
        public const int MaximumDataRows = 100;
        public const long MaximumFileBytes = 1024 * 1024;

        private const long MaximumExpandedBytes = 10 * 1024 * 1024;
        private const int MaximumArchiveEntries = 200;

        private static readonly string[] RequiredHeaders =
        [
            "Factor",
        "RiskLevel",
        "ComparisonOperator",
        "ThresholdValue",
        "Unit",
        "Description",
        "IsEnabled"
        ];

        public async Task<RiskThresholdExcelParseResult> ParseAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var errors = new List<RiskThresholdValidationError>();
            var candidates = new List<RiskThresholdCandidate>();
            var fileName = Path.GetFileName(file?.FileName ?? string.Empty);

            if (file is null)
            {
                errors.Add(new(0, "File", "Vui lòng chọn file Excel."));
                return new(fileName, 0, candidates, errors);
            }

            if (file.Length == 0)
            {
                errors.Add(new(0, "File", "File Excel không được để trống."));
                return new(fileName, 0, candidates, errors);
            }

            if (!string.Equals(
                    Path.GetExtension(fileName),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new(0, "File", "Chỉ chấp nhận file có định dạng .xlsx."));
                return new(fileName, 0, candidates, errors);
            }

            if (file.Length > MaximumFileBytes)
            {
                errors.Add(new(0, "File", "Dung lượng file không được vượt quá 1 MB."));
                return new(fileName, 0, candidates, errors);
            }

            await using var memoryStream = new MemoryStream();
            await CopyWithLimitAsync(file, memoryStream, errors, cancellationToken);

            if (errors.Count > 0)
                return new(fileName, 0, candidates, errors);

            memoryStream.Position = 0;

            if (!ValidateArchive(memoryStream, errors))
                return new(fileName, 0, candidates, errors);

            try
            {
                using var workbook = new XLWorkbook(memoryStream);

                if (workbook.Worksheets.Count != 1)
                {
                    errors.Add(new(0, "Worksheet",
                        $"File phải chứa đúng một sheet tên {WorksheetName}."));
                }

                var worksheet = workbook.Worksheets.FirstOrDefault(sheet =>
                    string.Equals(
                        sheet.Name,
                        WorksheetName,
                        StringComparison.Ordinal));

                if (worksheet is null)
                {
                    errors.Add(new(0, "Worksheet",
                        $"Không tìm thấy sheet {WorksheetName}."));

                    return new(fileName, 0, candidates, errors);
                }

                ValidateHeaders(worksheet, errors);

                if (errors.Count > 0)
                    return new(fileName, 0, candidates, errors);

                var lastColumn = worksheet
                    .LastColumnUsed(XLCellsUsedOptions.Contents)?
                    .ColumnNumber() ?? RequiredHeaders.Length;

                if (lastColumn > RequiredHeaders.Length)
                {
                    errors.Add(new(1, "Worksheet",
                        $"Sheet chỉ được chứa {RequiredHeaders.Length} cột theo template."));
                }

                var lastRow = worksheet
                    .LastRowUsed(XLCellsUsedOptions.Contents)?
                    .RowNumber() ?? 1;

                if (lastRow > MaximumDataRows + 1)
                {
                    errors.Add(new(0, "Worksheet",
                        $"Dữ liệu chỉ được nằm trong dòng 2 đến {MaximumDataRows + 1}."));

                    return new(fileName, 0, candidates, errors);
                }

                for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsEmptyDataRow(worksheet, rowNumber))
                        continue;

                    ValidateNoFormulas(worksheet, rowNumber, errors);

                    candidates.Add(new RiskThresholdCandidate(
                        rowNumber,
                        ReadText(worksheet, rowNumber, 1),
                        ReadText(worksheet, rowNumber, 2),
                        ReadText(worksheet, rowNumber, 3),
                        ReadDecimal(worksheet, rowNumber, 4),
                        ReadText(worksheet, rowNumber, 5),
                        ReadText(worksheet, rowNumber, 6),
                        ReadBoolean(worksheet, rowNumber, 7),
                        Version: 1));
                }

                if (candidates.Count == 0)
                {
                    errors.Add(new(0, "File",
                        "File không chứa dòng cấu hình ngưỡng nào."));
                }

                return new(fileName, candidates.Count, candidates, errors);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or ArgumentException ||
                exception.GetType().Namespace?.StartsWith(
                    "DocumentFormat.OpenXml",
                    StringComparison.Ordinal) == true)
            {
                errors.Add(new(0, "File",
                    "Không thể đọc file. File có thể bị hỏng hoặc không phải XLSX hợp lệ."));

                return new(fileName, 0, candidates, errors);
            }
        }

        /// Tạo template Excel từ cấu hình Version 1 hiện tại
        /// Template có dropdown để giảm lỗi nhập liệu thủ công
        public byte[] CreateTemplate(
            IReadOnlyList<ValidatedRiskThreshold> thresholds)
        {
            var versionOneThresholds = thresholds
                .Where(threshold => threshold.Version == 1)
                .OrderBy(threshold => GetFactorOrder(threshold.Factor))
                .ThenBy(threshold => GetRiskLevelOrder(threshold.RiskLevel))
                .ToList();

            if (versionOneThresholds.Count > MaximumDataRows)
            {
                throw new ArgumentException(
                    $"Template không được chứa quá {MaximumDataRows} dòng.",
                    nameof(thresholds));
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(WorksheetName);

            WriteHeaders(worksheet);

            var rowNumber = 2;

            foreach (var threshold in versionOneThresholds)
            {
                worksheet.Cell(rowNumber, 1).Value = threshold.Factor;
                worksheet.Cell(rowNumber, 2).Value = threshold.RiskLevel;
                worksheet.Cell(rowNumber, 3).Value =
                    ToExcelOperator(threshold.ComparisonOperator);
                worksheet.Cell(rowNumber, 4).Value = threshold.ThresholdValue;
                worksheet.Cell(rowNumber, 5).Value = threshold.Unit;
                worksheet.Cell(rowNumber, 6).Value = threshold.Description ?? string.Empty;
                worksheet.Cell(rowNumber, 7).Value = threshold.IsEnabled;

                rowNumber++;
            }

            ConfigureTemplateLayout(worksheet, rowNumber - 1);
            ConfigureTemplateValidation(worksheet);

            using var outputStream = new MemoryStream();
            workbook.SaveAs(outputStream);

            return outputStream.ToArray();
        }

        private static void WriteHeaders(IXLWorksheet worksheet)
        {
            for (var column = 1; column <= RequiredHeaders.Length; column++)
            {
                worksheet.Cell(1, column).Value = RequiredHeaders[column - 1];
            }

            var headerRange = worksheet.Range(
                1,
                1,
                1,
                RequiredHeaders.Length);

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F4C5C");
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 24;
        }

        private static void ConfigureTemplateLayout(
            IXLWorksheet worksheet,
            int lastDataRow)
        {
            // Đặt độ rộng cố định để template hiển thị ổn định trên Docker
            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 18;
            worksheet.Column(3).Width = 24;
            worksheet.Column(4).Width = 20;
            worksheet.Column(5).Width = 16;
            worksheet.Column(6).Width = 45;
            worksheet.Column(7).Width = 16;

            worksheet.Column(6).Style.Alignment.WrapText = true;

            worksheet
                .Range(2, 4, MaximumDataRows + 1, 4)
                .Style.NumberFormat.Format = "0.000";

            worksheet
                .Range(2, 7, MaximumDataRows + 1, 7)
                .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.SheetView.FreezeRows(1);

            // Giữ bộ lọc ngay cả khi database chưa có threshold
            var filterLastRow = Math.Max(2, lastDataRow);

            worksheet
                .Range(1, 1, filterLastRow, RequiredHeaders.Length)
                .SetAutoFilter();
        }

        private static void ConfigureTemplateValidation(
            IXLWorksheet worksheet)
        {
            var lastAllowedRow = MaximumDataRows + 1;

            AddListValidation(
                worksheet.Range(2, 1, lastAllowedRow, 1),
                "WIND,RAIN,VISIBILITY",
                "Chỉ chọn WIND, RAIN hoặc VISIBILITY.");

            AddListValidation(
                worksheet.Range(2, 2, lastAllowedRow, 2),
                "LOW,MEDIUM,HIGH,CRITICAL",
                "Chỉ chọn LOW, MEDIUM, HIGH hoặc CRITICAL.");

            AddListValidation(
                worksheet.Range(2, 3, lastAllowedRow, 3),
                ">=,<=",
                "Chỉ chọn toán tử >= hoặc <=.");

            AddListValidation(
                worksheet.Range(2, 7, lastAllowedRow, 7),
                "TRUE,FALSE",
                "Chỉ chọn TRUE hoặc FALSE.");
        }

        private static void AddListValidation(
            IXLRange range,
            string allowedValues,
            string errorMessage)
        {
            var validation = range.CreateDataValidation();

            validation.List($"\"{allowedValues}\"", inCellDropdown: true);
            validation.IgnoreBlanks = false;
            validation.ShowErrorMessage = true;
            validation.ErrorTitle = "Giá trị không hợp lệ";
            validation.ErrorMessage = errorMessage;
            validation.ShowInputMessage = true;
            validation.InputTitle = "PORMS";
            validation.InputMessage = errorMessage;
        }

        private static string ToExcelOperator(string comparisonOperator)
        {
            return comparisonOperator.Trim().ToUpperInvariant() switch
            {
                "GTE" or ">=" => ">=",
                "LTE" or "<=" => "<=",
                _ => throw new ArgumentException(
                    $"Toán tử {comparisonOperator} không được hỗ trợ.",
                    nameof(comparisonOperator))
            };
        }

        private static int GetFactorOrder(string factor)
        {
            return factor switch
            {
                "WIND" => 1,
                "RAIN" => 2,
                "VISIBILITY" => 3,
                _ => 99
            };
        }

        private static int GetRiskLevelOrder(string riskLevel)
        {
            return riskLevel switch
            {
                "LOW" => 1,
                "MEDIUM" => 2,
                "HIGH" => 3,
                "CRITICAL" => 4,
                _ => 99
            };
        }

        private static async Task CopyWithLimitAsync(
            IFormFile file,
            Stream destination,
            ICollection<RiskThresholdValidationError> errors,
            CancellationToken cancellationToken)
        {
            await using var source = file.OpenReadStream();
            var buffer = new byte[81920];
            long totalBytes = 0;

            while (true)
            {
                var bytesRead = await source.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

                if (bytesRead == 0)
                    break;

                totalBytes += bytesRead;

                if (totalBytes > MaximumFileBytes)
                {
                    errors.Add(new(0, "File",
                        "Dung lượng thực tế của file vượt quá 1 MB."));
                    return;
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }
        }

        private static bool ValidateArchive(
            Stream stream,
            ICollection<RiskThresholdValidationError> errors)
        {
            try
            {
                using var archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Read,
                    leaveOpen: true);

                if (archive.Entries.Count > MaximumArchiveEntries)
                {
                    errors.Add(new(0, "File",
                        "File Excel chứa quá nhiều thành phần nội bộ."));
                    return false;
                }

                long expandedBytes = 0;
                var containsContentTypes = false;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Equals(
                            "[Content_Types].xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        containsContentTypes = true;
                    }

                    if (entry.FullName.EndsWith(
                            "vbaProject.bin",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new(0, "File",
                            "File Excel có chứa macro và không được hỗ trợ."));
                        return false;
                    }

                    if (entry.Length > MaximumExpandedBytes - expandedBytes)
                    {
                        errors.Add(new(0, "File",
                            "Dung lượng giải nén của file vượt quá giới hạn cho phép."));
                        return false;
                    }

                    expandedBytes += entry.Length;
                }

                if (!containsContentTypes)
                {
                    errors.Add(new(0, "File",
                        "File không có cấu trúc XLSX hợp lệ."));
                    return false;
                }

                return true;
            }
            catch (InvalidDataException)
            {
                errors.Add(new(0, "File",
                    "File không có cấu trúc XLSX hợp lệ."));
                return false;
            }
            finally
            {
                stream.Position = 0;
            }
        }

        private static void ValidateHeaders(
            IXLWorksheet worksheet,
            ICollection<RiskThresholdValidationError> errors)
        {
            for (var column = 1; column <= RequiredHeaders.Length; column++)
            {
                var actualHeader = worksheet.Cell(1, column).GetString().Trim();
                var expectedHeader = RequiredHeaders[column - 1];

                if (!string.Equals(
                        actualHeader,
                        expectedHeader,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new(
                        1,
                        expectedHeader,
                        $"Cột {column} phải có tiêu đề {expectedHeader}."));
                }
            }
        }

        private static void ValidateNoFormulas(
            IXLWorksheet worksheet,
            int rowNumber,
            ICollection<RiskThresholdValidationError> errors)
        {
            for (var column = 1; column <= RequiredHeaders.Length; column++)
            {
                if (worksheet.Cell(rowNumber, column).HasFormula)
                {
                    errors.Add(new(
                        rowNumber,
                        RequiredHeaders[column - 1],
                        "Không chấp nhận công thức Excel trong vùng dữ liệu."));
                }
            }
        }

        private static bool IsEmptyDataRow(
            IXLWorksheet worksheet,
            int rowNumber)
        {
            for (var column = 1; column <= RequiredHeaders.Length; column++)
            {
                if (!worksheet.Cell(rowNumber, column).IsEmpty())
                    return false;
            }

            return true;
        }

        private static string? ReadText(
            IXLWorksheet worksheet,
            int rowNumber,
            int columnNumber)
        {
            var text = worksheet.Cell(rowNumber, columnNumber).GetString().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static decimal? ReadDecimal(
            IXLWorksheet worksheet,
            int rowNumber,
            int columnNumber)
        {
            var cell = worksheet.Cell(rowNumber, columnNumber);

            return cell.TryGetValue<decimal>(out var value)
                ? value
                : null;
        }

        private static bool? ReadBoolean(
            IXLWorksheet worksheet,
            int rowNumber,
            int columnNumber)
        {
            var cell = worksheet.Cell(rowNumber, columnNumber);

            if (cell.TryGetValue<bool>(out var booleanValue))
                return booleanValue;

            return cell.GetString().Trim().ToUpperInvariant() switch
            {
                "TRUE" => true,
                "FALSE" => false,
                _ => null
            };
        }
    }

    public sealed record RiskThresholdExcelParseResult(
        string FileName,
        int TotalRows,
        IReadOnlyList<RiskThresholdCandidate> Candidates,
        IReadOnlyList<RiskThresholdValidationError> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }
}