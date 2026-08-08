using ClosedXML.Excel;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;

namespace PORMS.API.Services
{
    /// Đọc và tạo file Excel cho cấu hình SOP
    /// Service này chỉ xử lý file, không truy cập hoặc ghi database
    public sealed class SopRuleExcelService
    {
        public const string WorksheetName = "SOPRules";
        public const int MaximumDataRows = 200;
        public const long MaximumFileBytes = 1024 * 1024;

        private const long MaximumExpandedBytes = 10 * 1024 * 1024;
        private const int MaximumArchiveEntries = 200;

        private static readonly string[] RequiredHeaders =
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

        /// Phân tích file Excel thành các dòng dữ liệu thô
        /// Validation nghiệp vụ chi tiết do SopRuleImportValidator đảm nhiệm
        public async Task<SopRuleExcelParseResult> ParseAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var errors = new List<SopRuleImportValidationError>();
            var candidates = new List<SopRuleImportCandidate>();
            var fileName = Path.GetFileName(file?.FileName ?? string.Empty);

            if (file is null)
            {
                errors.Add(new(
                    0,
                    "File",
                    "Vui lòng chọn file Excel."));

                return new(fileName, 0, candidates, errors);
            }

            if (file.Length == 0)
            {
                errors.Add(new(
                    0,
                    "File",
                    "File Excel không được để trống."));

                return new(fileName, 0, candidates, errors);
            }

            if (!string.Equals(
                    Path.GetExtension(fileName),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new(
                    0,
                    "File",
                    "Chỉ chấp nhận file có định dạng .xlsx."));

                return new(fileName, 0, candidates, errors);
            }

            if (file.Length > MaximumFileBytes)
            {
                errors.Add(new(
                    0,
                    "File",
                    "Dung lượng file không được vượt quá 1 MB."));

                return new(fileName, 0, candidates, errors);
            }

            await using var memoryStream = new MemoryStream();

            await CopyWithLimitAsync(
                file,
                memoryStream,
                errors,
                cancellationToken);

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
                    errors.Add(new(
                        0,
                        "Worksheet",
                        $"File phải chứa đúng một sheet tên {WorksheetName}."));
                }

                var worksheet = workbook.Worksheets.FirstOrDefault(sheet =>
                    string.Equals(
                        sheet.Name,
                        WorksheetName,
                        StringComparison.Ordinal));

                if (worksheet is null)
                {
                    errors.Add(new(
                        0,
                        "Worksheet",
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
                    errors.Add(new(
                        1,
                        "Worksheet",
                        $"Sheet chỉ được chứa {RequiredHeaders.Length} cột theo template."));
                }

                var lastRow = worksheet
                    .LastRowUsed(XLCellsUsedOptions.Contents)?
                    .RowNumber() ?? 1;

                if (lastRow > MaximumDataRows + 1)
                {
                    errors.Add(new(
                        0,
                        "Worksheet",
                        $"Dữ liệu chỉ được nằm trong dòng 2 đến {MaximumDataRows + 1}."));

                    return new(fileName, 0, candidates, errors);
                }

                for (var rowNumber = 2;
                     rowNumber <= lastRow;
                     rowNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsEmptyDataRow(worksheet, rowNumber))
                        continue;

                    ValidateNoFormulas(
                        worksheet,
                        rowNumber,
                        errors);

                    var executionOrder = ReadOptionalInteger(
                        worksheet,
                        rowNumber,
                        9,
                        errors);

                    candidates.Add(new SopRuleImportCandidate(
                        RowNumber: rowNumber,
                        RuleCode: ReadText(worksheet, rowNumber, 1),
                        RuleName: ReadText(worksheet, rowNumber, 2),
                        Description: ReadText(worksheet, rowNumber, 3),
                        TriggerRiskLevel: ReadText(worksheet, rowNumber, 4),
                        PreviousRiskLevel: ReadText(worksheet, rowNumber, 5),
                        AppliesToZoneType: ReadText(worksheet, rowNumber, 6),
                        ActionType: ReadText(worksheet, rowNumber, 7),
                        ActionConfigJson: ReadText(worksheet, rowNumber, 8),
                        ExecutionOrder: executionOrder,
                        IsActive: ReadBoolean(worksheet, rowNumber, 10)));
                }

                if (candidates.Count == 0)
                {
                    errors.Add(new(
                        0,
                        "File",
                        "File không chứa dòng quy tắc SOP nào."));
                }

                return new(
                    fileName,
                    candidates.Count,
                    candidates,
                    errors);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or ArgumentException ||
                exception.GetType().Namespace?.StartsWith(
                    "DocumentFormat.OpenXml",
                    StringComparison.Ordinal) == true)
            {
                errors.Add(new(
                    0,
                    "File",
                    "Không thể đọc file. File có thể bị hỏng hoặc không phải XLSX hợp lệ."));

                return new(fileName, 0, candidates, errors);
            }
        }

        /// Tạo template chứa các SOP hiện tại
        /// Người dùng có thể chỉnh sửa dòng cũ hoặc thêm RuleCode mới
        public byte[] CreateTemplate(
            IReadOnlyList<ValidatedSopRuleImport> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            if (rules.Count > MaximumDataRows)
            {
                throw new ArgumentException(
                    $"Template không được chứa quá {MaximumDataRows} dòng.",
                    nameof(rules));
            }

            var orderedRules = rules
                .OrderBy(rule => GetRiskLevelOrder(rule.TriggerRiskLevel))
                .ThenBy(rule => rule.ExecutionOrder)
                .ThenBy(rule => rule.RuleCode, StringComparer.Ordinal)
                .ToList();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(WorksheetName);

            WriteHeaders(worksheet);

            var rowNumber = 2;

            foreach (var rule in orderedRules)
            {
                worksheet.Cell(rowNumber, 1).Value = rule.RuleCode;
                worksheet.Cell(rowNumber, 2).Value = rule.RuleName;
                worksheet.Cell(rowNumber, 3).Value =
                    rule.Description ?? string.Empty;

                worksheet.Cell(rowNumber, 4).Value =
                    rule.TriggerRiskLevel;

                // ANY và ALL giúp người dùng hiểu rõ ý nghĩa của giá trị NULL
                worksheet.Cell(rowNumber, 5).Value =
                    rule.PreviousRiskLevel ?? "ANY";

                worksheet.Cell(rowNumber, 6).Value =
                    rule.AppliesToZoneType ?? "ALL";

                worksheet.Cell(rowNumber, 7).Value =
                    rule.ActionType;

                worksheet.Cell(rowNumber, 8).Value =
                    rule.ActionConfigJson;

                worksheet.Cell(rowNumber, 9).Value =
                    rule.ExecutionOrder;

                worksheet.Cell(rowNumber, 10).Value =
                    rule.IsActive;

                rowNumber++;
            }

            ConfigureTemplateLayout(
                worksheet,
                rowNumber - 1);

            ConfigureTemplateValidation(worksheet);

            using var outputStream = new MemoryStream();
            workbook.SaveAs(outputStream);

            return outputStream.ToArray();
        }

        private static void WriteHeaders(IXLWorksheet worksheet)
        {
            for (var column = 1;
                 column <= RequiredHeaders.Length;
                 column++)
            {
                worksheet.Cell(1, column).Value =
                    RequiredHeaders[column - 1];
            }

            var headerRange = worksheet.Range(
                1,
                1,
                1,
                RequiredHeaders.Length);

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#0F4C5C");

            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 26;
        }

        private static void ConfigureTemplateLayout(
            IXLWorksheet worksheet,
            int lastDataRow)
        {
            // Độ rộng cố định giúp template hiển thị ổn định
            worksheet.Column(1).Width = 24;
            worksheet.Column(2).Width = 32;
            worksheet.Column(3).Width = 48;
            worksheet.Column(4).Width = 22;
            worksheet.Column(5).Width = 22;
            worksheet.Column(6).Width = 22;
            worksheet.Column(7).Width = 24;
            worksheet.Column(8).Width = 55;
            worksheet.Column(9).Width = 18;
            worksheet.Column(10).Width = 14;

            // Hai cột kỹ thuật vẫn được giữ để tương thích file cũ.
            // Người dùng thông thường có thể để trống để hệ thống tự sinh.
            worksheet.Column(8).Hide();
            worksheet.Column(9).Hide();

            worksheet.Column(3).Style.Alignment.WrapText = true;
            worksheet.Column(8).Style.Alignment.WrapText = true;

            worksheet
                .Range(2, 9, MaximumDataRows + 1, 9)
                .Style.NumberFormat.Format = "0";

            worksheet
                .Range(2, 9, MaximumDataRows + 1, 10)
                .Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.SheetView.FreezeRows(1);

            // Duy trì bộ lọc ngay cả khi database chưa có SOP
            var filterLastRow = Math.Max(2, lastDataRow);

            worksheet
                .Range(
                    1,
                    1,
                    filterLastRow,
                    RequiredHeaders.Length)
                .SetAutoFilter();
        }

        private static void ConfigureTemplateValidation(
            IXLWorksheet worksheet)
        {
            var lastAllowedRow = MaximumDataRows + 1;

            AddListValidation(
                worksheet.Range(2, 4, lastAllowedRow, 4),
                "LOW,MEDIUM,HIGH,CRITICAL",
                "Chỉ chọn LOW, MEDIUM, HIGH hoặc CRITICAL.",
                allowBlank: false);

            AddListValidation(
                worksheet.Range(2, 5, lastAllowedRow, 5),
                "ANY,LOW,MEDIUM,HIGH,CRITICAL",
                "Chọn ANY nếu quy tắc không yêu cầu mức rủi ro trước đó.",
                allowBlank: true);

            AddListValidation(
                worksheet.Range(2, 6, lastAllowedRow, 6),
                "ALL,DOCK,YARD,GATE,WAREHOUSE",
                "Chọn ALL hoặc một loại khu vực hợp lệ.",
                allowBlank: true);

            AddListValidation(
                worksheet.Range(2, 7, lastAllowedRow, 7),
                "CREATE_TASK,SEND_ALERT,RESTRICT_ZONE,UNRESTRICT_ZONE,SET_NORMAL_MODE,SET_LIMITED_MODE,STOP_OPERATIONS",
                "Chọn một ActionType được PORMS hỗ trợ.",
                allowBlank: false);

            AddListValidation(
                worksheet.Range(2, 10, lastAllowedRow, 10),
                "TRUE,FALSE",
                "Chỉ chọn TRUE hoặc FALSE.",
                allowBlank: false);
        }

        private static void AddListValidation(
            IXLRange range,
            string allowedValues,
            string message,
            bool allowBlank)
        {
            var validation = range.CreateDataValidation();

            validation.List(
                $"\"{allowedValues}\"",
                inCellDropdown: true);

            validation.IgnoreBlanks = allowBlank;
            validation.ShowErrorMessage = true;
            validation.ErrorTitle = "Giá trị không hợp lệ";
            validation.ErrorMessage = message;
            validation.ShowInputMessage = true;
            validation.InputTitle = "PORMS";
            validation.InputMessage = message;
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
            ICollection<SopRuleImportValidationError> errors,
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
                    errors.Add(new(
                        0,
                        "File",
                        "Dung lượng thực tế của file vượt quá 1 MB."));

                    return;
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }
        }

        /// Kiểm tra cấu trúc ZIP của XLSX trước khi ClosedXML giải nén file
        private static bool ValidateArchive(
            Stream stream,
            ICollection<SopRuleImportValidationError> errors)
        {
            try
            {
                using var archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Read,
                    leaveOpen: true);

                if (archive.Entries.Count > MaximumArchiveEntries)
                {
                    errors.Add(new(
                        0,
                        "File",
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
                        errors.Add(new(
                            0,
                            "File",
                            "File Excel có chứa macro và không được hỗ trợ."));

                        return false;
                    }

                    if (entry.Length >
                        MaximumExpandedBytes - expandedBytes)
                    {
                        errors.Add(new(
                            0,
                            "File",
                            "Dung lượng giải nén của file vượt quá giới hạn cho phép."));

                        return false;
                    }

                    expandedBytes += entry.Length;
                }

                if (!containsContentTypes)
                {
                    errors.Add(new(
                        0,
                        "File",
                        "File không có cấu trúc XLSX hợp lệ."));

                    return false;
                }

                return true;
            }
            catch (InvalidDataException)
            {
                errors.Add(new(
                    0,
                    "File",
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
            ICollection<SopRuleImportValidationError> errors)
        {
            for (var column = 1;
                 column <= RequiredHeaders.Length;
                 column++)
            {
                var actualHeader = worksheet
                    .Cell(1, column)
                    .GetString()
                    .Trim();

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
            ICollection<SopRuleImportValidationError> errors)
        {
            for (var column = 1;
                 column <= RequiredHeaders.Length;
                 column++)
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
            for (var column = 1;
                 column <= RequiredHeaders.Length;
                 column++)
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
            var text = worksheet
                .Cell(rowNumber, columnNumber)
                .GetString()
                .Trim();

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text;
        }

        private static int? ReadOptionalInteger(
            IXLWorksheet worksheet,
            int rowNumber,
            int columnNumber,
            ICollection<SopRuleImportValidationError> errors)
        {
            var cell = worksheet.Cell(rowNumber, columnNumber);

            if (cell.IsEmpty())
                return null;

            if (cell.TryGetValue<int>(out var integerValue))
                return integerValue;

            if (cell.TryGetValue<decimal>(out var decimalValue) &&
                decimalValue == decimal.Truncate(decimalValue) &&
                decimalValue is >= int.MinValue and <= int.MaxValue)
            {
                return (int)decimalValue;
            }

            errors.Add(new(
                rowNumber,
                "ExecutionOrder",
                "ExecutionOrder phải là số nguyên."));

            return null;
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

    public sealed record SopRuleExcelParseResult(
        string FileName,
        int TotalRows,
        IReadOnlyList<SopRuleImportCandidate> Candidates,
        IReadOnlyList<SopRuleImportValidationError> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }
}
