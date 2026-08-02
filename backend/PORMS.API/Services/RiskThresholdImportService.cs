using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services
{
    /// Điều phối quá trình tạo template và xem trước import
    /// Việc đọc Excel, validation và truy cập database được tách riêng
    public sealed class RiskThresholdImportService
    {
        private readonly RiskThresholdExcelService _excelService;
        private readonly RiskThresholdValidator _validator;
        private readonly RiskRepository _repository;

        public RiskThresholdImportService(
            RiskThresholdExcelService excelService,
            RiskThresholdValidator validator,
            RiskRepository repository)
        {
            _excelService = excelService;
            _validator = validator;
            _repository = repository;
        }

        public async Task<byte[]> CreateTemplateAsync(
            CancellationToken cancellationToken)
        {
            var current = await _repository
                .GetVersionOneThresholdsAsync(cancellationToken);

            var validation = _validator.ValidateRows(
                current.Select(ToCurrentCandidate).ToList());

            var configurationErrors = validation.IsValid
                ? _validator.ValidateConfiguration(validation.ValidRows)
                : [];

            if (!validation.IsValid || configurationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cấu hình ngưỡng hiện tại không hợp lệ để tạo template.");
            }

            return _excelService.CreateTemplate(validation.ValidRows);
        }

        public async Task<RiskThresholdImportPreviewResponse> PreviewAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var result = await BuildPreviewAsync(file, cancellationToken);
            return result.Preview;
        }

        /// Xác nhận import sau khi kiểm tra lại toàn bộ file trên server
        /// Không sử dụng kết quả preview cũ vì file hoặc database có thể đã thay đổi
        public async Task<RiskThresholdImportExecutionResult> ImportAsync(
            IFormFile? file,
            string? changeReason,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            if (actorUserId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Không xác định được người thực hiện import.",
                    nameof(actorUserId));
            }

            var previewResult = await BuildPreviewAsync(
                file,
                cancellationToken);

            var preview = previewResult.Preview;
            var normalizedReason = changeReason?.Trim();

            // Lý do thay đổi giúp truy vết thao tác trong operation log
            if (string.IsNullOrWhiteSpace(normalizedReason))
            {
                preview = AddTopLevelError(
                    preview,
                    "ChangeReason",
                    "Vui lòng nhập lý do thay đổi.");
            }
            else if (normalizedReason.Length < 5)
            {
                preview = AddTopLevelError(
                    preview,
                    "ChangeReason",
                    "Lý do thay đổi phải có ít nhất 5 ký tự.");
            }
            else if (normalizedReason.Length > 500)
            {
                preview = AddTopLevelError(
                    preview,
                    "ChangeReason",
                    "Lý do thay đổi không được vượt quá 500 ký tự.");
            }

            // Không ghi một phần file nếu còn bất kỳ lỗi nào
            if (!preview.CanImport)
            {
                return new RiskThresholdImportExecutionResult(
                    false,
                    preview,
                    null,
                    null);
            }

            var changedRowNumbers = preview.Rows
                .Where(row =>
                    row.Action is "CREATE" or "UPDATE")
                .Select(row => row.RowNumber)
                .ToHashSet();

            // Chỉ ghi những dòng thật sự tạo mới hoặc thay đổi dữ liệu
            var thresholdsToSave = previewResult.ImportedRows
                .Where(row => changedRowNumbers.Contains(row.RowNumber))
                .Select(row => new SaveRiskThresholdReadModel(
                    row.Factor,
                    row.RiskLevel,
                    row.ComparisonOperator,
                    row.ThresholdValue,
                    row.Unit,
                    row.Description,
                    row.Version,
                    row.IsEnabled))
                .ToList();

            var importBatchId = await _repository.ImportThresholdsAsync(
                thresholdsToSave,
                new RiskThresholdImportAuditReadModel(
                    actorUserId,
                    preview.FileName,
                    normalizedReason!,
                    preview.CreateCount,
                    preview.UpdateCount,
                    preview.UnchangedCount),
                cancellationToken);

            var configuration = await _repository.GetConfigAsync(
                cancellationToken);

            return new RiskThresholdImportExecutionResult(
                true,
                preview,
                importBatchId,
                configuration);
        }

        private async Task<PreviewBuildResult> BuildPreviewAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var parseResult = await _excelService.ParseAsync(
                file,
                cancellationToken);

            var importedValidation =
                _validator.ValidateRows(parseResult.Candidates);

            var current = await _repository
                .GetVersionOneThresholdsAsync(cancellationToken);

            var currentValidation = _validator.ValidateRows(
                current.Select(ToCurrentCandidate).ToList());

            var errors = new List<RiskThresholdValidationError>();
            errors.AddRange(parseResult.Errors);
            errors.AddRange(importedValidation.Errors);

            // Database hiện tại cũng phải hợp lệ trước khi áp dụng thay đổi mới
            if (!currentValidation.IsValid)
                errors.AddRange(currentValidation.Errors);

            if (parseResult.IsValid &&
                importedValidation.IsValid &&
                currentValidation.IsValid)
            {
                var mergedConfiguration = MergeConfiguration(
                    currentValidation.ValidRows,
                    importedValidation.ValidRows);

                errors.AddRange(
                    _validator.ValidateConfiguration(mergedConfiguration));
            }

            var distinctErrors = errors
                .GroupBy(error => new
                {
                    error.RowNumber,
                    error.Column,
                    error.Message
                })
                .Select(group => group.First())
                .ToList();

            var currentMap = currentValidation.ValidRows.ToDictionary(
                ThresholdKey,
                StringComparer.Ordinal);

            var importedMap = importedValidation.ValidRows.ToDictionary(
                row => row.RowNumber);

            var candidateRowNumbers = parseResult.Candidates
                .Select(row => row.RowNumber)
                .ToHashSet();

            var previewRows = parseResult.Candidates
                .Select(candidate => CreatePreviewRow(
                    candidate,
                    importedMap,
                    currentMap,
                    distinctErrors))
                .ToList();

            var topLevelErrors = distinctErrors
                .Where(error =>
                    error.RowNumber <= 1 ||
                    !candidateRowNumbers.Contains(error.RowNumber))
                .Select(ToErrorResponse)
                .ToList();

            var preview = new RiskThresholdImportPreviewResponse
            {
                FileName = parseResult.FileName,
                TotalRows = parseResult.TotalRows,
                ValidRows = previewRows.Count(row => row.Action != "INVALID"),
                InvalidRows = previewRows.Count(row => row.Action == "INVALID"),
                CreateCount = previewRows.Count(row => row.Action == "CREATE"),
                UpdateCount = previewRows.Count(row => row.Action == "UPDATE"),
                UnchangedCount = previewRows.Count(row => row.Action == "UNCHANGED"),
                CanImport = parseResult.TotalRows > 0 &&
                            distinctErrors.Count == 0,
                Rows = previewRows,
                Errors = topLevelErrors
            };

            return new PreviewBuildResult(
                preview,
                importedValidation.ValidRows);
        }

        private static RiskThresholdImportRowResponse CreatePreviewRow(
            RiskThresholdCandidate candidate,
            IReadOnlyDictionary<int, ValidatedRiskThreshold> importedMap,
            IReadOnlyDictionary<string, ValidatedRiskThreshold> currentMap,
            IReadOnlyList<RiskThresholdValidationError> allErrors)
        {
            var rowErrors = allErrors
                .Where(error => error.RowNumber == candidate.RowNumber)
                .Select(ToErrorResponse)
                .ToList();

            importedMap.TryGetValue(
                candidate.RowNumber,
                out var imported);

            ValidatedRiskThreshold? existing = null;

            if (imported is not null)
                currentMap.TryGetValue(ThresholdKey(imported), out existing);

            var action = rowErrors.Count > 0
                ? "INVALID"
                : existing is null
                    ? "CREATE"
                    : AreEquivalent(existing, imported!)
                        ? "UNCHANGED"
                        : "UPDATE";

            return new RiskThresholdImportRowResponse
            {
                RowNumber = candidate.RowNumber,
                Action = action,
                Factor = imported?.Factor ?? candidate.Factor,
                RiskLevel = imported?.RiskLevel ?? candidate.RiskLevel,
                ComparisonOperator = imported is null
                    ? candidate.ComparisonOperator
                    : ToUiOperator(imported.ComparisonOperator),
                ThresholdValue =
                    imported?.ThresholdValue ?? candidate.ThresholdValue,
                Unit = imported?.Unit ?? candidate.Unit,
                Description =
                    imported?.Description ?? candidate.Description,
                IsEnabled =
                    imported?.IsEnabled ?? candidate.IsEnabled,
                ExistingValue = existing is null
                    ? null
                    : new RiskThresholdImportExistingValueResponse
                    {
                        ComparisonOperator =
                            ToUiOperator(existing.ComparisonOperator),
                        ThresholdValue = existing.ThresholdValue,
                        Unit = existing.Unit,
                        Description = existing.Description,
                        IsEnabled = existing.IsEnabled
                    },
                Errors = rowErrors
            };
        }

        private static IReadOnlyList<ValidatedRiskThreshold>
            MergeConfiguration(
                IReadOnlyList<ValidatedRiskThreshold> current,
                IReadOnlyList<ValidatedRiskThreshold> imported)
        {
            var merged = current.ToDictionary(
                ThresholdKey,
                StringComparer.Ordinal);

            // Dòng trong file thay thế giá trị hiện tại có cùng Factor/RiskLevel
            foreach (var threshold in imported)
                merged[ThresholdKey(threshold)] = threshold;

            return merged.Values.ToList();
        }

        private static RiskThresholdCandidate ToCurrentCandidate(
            RiskThresholdReadModel threshold)
        {
            return new RiskThresholdCandidate(
                0,
                threshold.Factor,
                threshold.RiskLevel,
                threshold.ComparisonOperator,
                threshold.ThresholdValue,
                threshold.Unit,
                threshold.Description,
                threshold.IsEnabled,
                threshold.Version);
        }

        private static bool AreEquivalent(
            ValidatedRiskThreshold existing,
            ValidatedRiskThreshold imported)
        {
            return existing.ComparisonOperator == imported.ComparisonOperator &&
                   existing.ThresholdValue == imported.ThresholdValue &&
                   string.Equals(
                       existing.Unit,
                       imported.Unit,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       NormalizeDescription(existing.Description),
                       NormalizeDescription(imported.Description),
                       StringComparison.Ordinal) &&
                   existing.IsEnabled == imported.IsEnabled;
        }

        private static string ThresholdKey(
            ValidatedRiskThreshold threshold) =>
            $"{threshold.Factor}:{threshold.RiskLevel}";

        private static string ToUiOperator(string value)
        {
            return value.Trim().ToUpperInvariant() switch
            {
                "GTE" or ">=" => ">=",
                "LTE" or "<=" => "<=",
                _ => value
            };
        }

        private static string? NormalizeDescription(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// Thêm lỗi cấp request vào preview mà không thay đổi dữ liệu gốc
        private static RiskThresholdImportPreviewResponse AddTopLevelError(
            RiskThresholdImportPreviewResponse preview,
            string column,
            string message)
        {
            var errors = preview.Errors
                .Append(new RiskThresholdImportErrorResponse
                {
                    RowNumber = 0,
                    Column = column,
                    Message = message
                })
                .ToList();

            return new RiskThresholdImportPreviewResponse
            {
                FileName = preview.FileName,
                TotalRows = preview.TotalRows,
                ValidRows = preview.ValidRows,
                InvalidRows = preview.InvalidRows,
                CreateCount = preview.CreateCount,
                UpdateCount = preview.UpdateCount,
                UnchangedCount = preview.UnchangedCount,
                CanImport = false,
                Rows = preview.Rows,
                Errors = errors
            };
        }

        private static RiskThresholdImportErrorResponse ToErrorResponse(
            RiskThresholdValidationError error)
        {
            return new RiskThresholdImportErrorResponse
            {
                RowNumber = error.RowNumber,
                Column = error.Column,
                Message = error.Message
            };
        }

        private sealed record PreviewBuildResult(
            RiskThresholdImportPreviewResponse Preview,
            IReadOnlyList<ValidatedRiskThreshold> ImportedRows);
    }

    /// Kết quả nội bộ của thao tác xác nhận import
    /// Controller sẽ chuyển Configuration thành DTO trả về frontend
    public sealed record RiskThresholdImportExecutionResult(
        bool IsSuccess,
        RiskThresholdImportPreviewResponse Preview,
        Guid? ImportBatchId,
        RiskConfigReadModel? Configuration);
}
