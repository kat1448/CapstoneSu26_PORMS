using System.Text.Json.Nodes;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services
{
    /// Điều phối quá trình tạo template, preview và import SOP
    /// Parser, validator và repository được giữ tách biệt để dễ kiểm thử
    public sealed class SopRuleImportService
    {
        private readonly SopRuleExcelService _excelService;
        private readonly SopRuleImportValidator _validator;
        private readonly SopRuleRepository _repository;

        public SopRuleImportService(
            SopRuleExcelService excelService,
            SopRuleImportValidator validator,
            SopRuleRepository repository)
        {
            _excelService = excelService;
            _validator = validator;
            _repository = repository;
        }

        /// Tạo template từ toàn bộ SOP chưa bị xóa trong database
        public async Task<byte[]> CreateTemplateAsync(
            CancellationToken cancellationToken)
        {
            var currentRules =
                await _repository.GetImportRulesAsync(cancellationToken);

            var validation = _validator.ValidateRows(
                currentRules
                    .Select(ToCurrentCandidate)
                    .ToList());

            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "Cấu hình SOP hiện tại không hợp lệ để tạo template.");
            }

            return _excelService.CreateTemplate(validation.ValidRows);
        }

        /// Preview không ghi hoặc thay đổi dữ liệu trong database
        public async Task<SopRuleImportPreviewResponse> PreviewAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var result =
                await BuildPreviewAsync(file, cancellationToken);

            return result.Preview;
        }

        /// Kiểm tra lại file và database trước khi xác nhận import
        public async Task<SopRuleImportExecutionResult> ImportAsync(
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

            var previewResult =
                await BuildPreviewAsync(file, cancellationToken);

            var preview = previewResult.Preview;
            var normalizedReason = changeReason?.Trim();

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
                return new SopRuleImportExecutionResult(
                    IsSuccess: false,
                    Preview: preview,
                    ImportBatchId: null,
                    Configuration: null);
            }

            var changedActions = preview.Rows
                .Where(row =>
                    row.Action is "CREATE" or "UPDATE")
                .ToDictionary(
                    row => row.RowNumber,
                    row => row.Action);

            // Chỉ gửi các dòng thật sự thay đổi xuống repository
            var rulesToSave = previewResult.ImportedRows
                .Where(rule =>
                    changedActions.ContainsKey(rule.RowNumber))
                .Select(rule => new SaveSopRuleImportReadModel(
                    ExpectedAction: changedActions[rule.RowNumber],
                    RuleCode: rule.RuleCode,
                    RuleName: rule.RuleName,
                    Description: rule.Description,
                    TriggerRiskLevel: rule.TriggerRiskLevel,
                    PreviousRiskLevel: rule.PreviousRiskLevel,
                    AppliesToZoneType: rule.AppliesToZoneType,
                    ActionType: rule.ActionType,
                    ActionConfigJson: rule.ActionConfigJson,
                    ExecutionOrder: rule.ExecutionOrder,
                    IsActive: rule.IsActive))
                .ToList();

            var importBatchId =
                await _repository.ImportRulesAsync(
                    rulesToSave,
                    new SopRuleImportAuditReadModel(
                        ActorUserId: actorUserId,
                        FileName: preview.FileName,
                        ChangeReason: normalizedReason!,
                        CreatedCount: preview.CreateCount,
                        UpdatedCount: preview.UpdateCount,
                        UnchangedCount: preview.UnchangedCount),
                    cancellationToken);

            var configuration =
                await _repository.GetRulesAsync(cancellationToken);

            return new SopRuleImportExecutionResult(
                IsSuccess: true,
                Preview: preview,
                ImportBatchId: importBatchId,
                Configuration: configuration);
        }

        private async Task<SopRulePreviewBuildResult> BuildPreviewAsync(
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            var parseResult =
                await _excelService.ParseAsync(
                    file,
                    cancellationToken);

            var importedValidation =
                _validator.ValidateRows(parseResult.Candidates);

            var currentRules =
                await _repository.GetImportRulesAsync(cancellationToken);

            var currentValidation = _validator.ValidateRows(
                currentRules
                    .Select(ToCurrentCandidate)
                    .ToList());

            var errors =
                new List<SopRuleImportValidationError>();

            errors.AddRange(parseResult.Errors);
            errors.AddRange(importedValidation.Errors);

            // Không import vào một cấu hình hiện tại đang không hợp lệ
            if (!currentValidation.IsValid)
                errors.AddRange(currentValidation.Errors);

            var distinctErrors = errors
                .GroupBy(error => new
                {
                    error.RowNumber,
                    error.Column,
                    error.Message
                })
                .Select(group => group.First())
                .ToList();

            var importedMap = importedValidation.ValidRows
                .ToDictionary(rule => rule.RowNumber);

            var currentValidatedMap =
                currentValidation.ValidRows.ToDictionary(
                    RuleKey,
                    StringComparer.Ordinal);

            var currentRecordMap = currentRules.ToDictionary(
                rule => NormalizeRuleCode(rule.RuleCode),
                StringComparer.Ordinal);

            var candidateRowNumbers = parseResult.Candidates
                .Select(candidate => candidate.RowNumber)
                .ToHashSet();

            var previewRows = parseResult.Candidates
                .Select(candidate => CreatePreviewRow(
                    candidate,
                    importedMap,
                    currentValidatedMap,
                    currentRecordMap,
                    distinctErrors))
                .ToList();

            var topLevelErrors = distinctErrors
                .Where(error =>
                    error.RowNumber <= 1 ||
                    !candidateRowNumbers.Contains(error.RowNumber))
                .Select(ToErrorResponse)
                .ToList();

            var preview = new SopRuleImportPreviewResponse
            {
                FileName = parseResult.FileName,
                TotalRows = parseResult.TotalRows,

                ValidRows = previewRows.Count(row =>
                    row.Action != "INVALID"),

                InvalidRows = previewRows.Count(row =>
                    row.Action == "INVALID"),

                CreateCount = previewRows.Count(row =>
                    row.Action == "CREATE"),

                UpdateCount = previewRows.Count(row =>
                    row.Action == "UPDATE"),

                UnchangedCount = previewRows.Count(row =>
                    row.Action == "UNCHANGED"),

                CanImport =
                    parseResult.TotalRows > 0 &&
                    distinctErrors.Count == 0,

                Rows = previewRows,
                Errors = topLevelErrors
            };

            return new SopRulePreviewBuildResult(
                Preview: preview,
                ImportedRows: importedValidation.ValidRows);
        }

        private static SopRuleImportRowResponse CreatePreviewRow(
            SopRuleImportCandidate candidate,
            IReadOnlyDictionary<int, ValidatedSopRuleImport> importedMap,
            IReadOnlyDictionary<string, ValidatedSopRuleImport>
                currentValidatedMap,
            IReadOnlyDictionary<string, SopRuleImportReadModel>
                currentRecordMap,
            IReadOnlyList<SopRuleImportValidationError> allErrors)
        {
            var rowErrors = allErrors
                .Where(error =>
                    error.RowNumber == candidate.RowNumber)
                .Select(ToErrorResponse)
                .ToList();

            importedMap.TryGetValue(
                candidate.RowNumber,
                out var imported);

            ValidatedSopRuleImport? existingValidated = null;
            SopRuleImportReadModel? existingRecord = null;

            if (imported is not null)
            {
                var ruleKey = RuleKey(imported);

                currentValidatedMap.TryGetValue(
                    ruleKey,
                    out existingValidated);

                currentRecordMap.TryGetValue(
                    ruleKey,
                    out existingRecord);
            }

            var action = rowErrors.Count > 0
                ? "INVALID"
                : existingValidated is null
                    ? "CREATE"
                    : AreEquivalent(existingValidated, imported!)
                        ? "UNCHANGED"
                        : "UPDATE";

            return new SopRuleImportRowResponse
            {
                RowNumber = candidate.RowNumber,
                Action = action,

                RuleCode =
                    imported?.RuleCode ?? candidate.RuleCode,

                RuleName =
                    imported?.RuleName ?? candidate.RuleName,

                Description =
                    imported?.Description ?? candidate.Description,

                TriggerRiskLevel =
                    imported?.TriggerRiskLevel ??
                    candidate.TriggerRiskLevel,

                PreviousRiskLevel =
                    imported?.PreviousRiskLevel ??
                    NormalizeDisplayOptional(
                        candidate.PreviousRiskLevel),

                AppliesToZoneType =
                    imported?.AppliesToZoneType ??
                    NormalizeDisplayOptional(
                        candidate.AppliesToZoneType),

                ActionType =
                    imported?.ActionType ?? candidate.ActionType,

                ActionConfigJson =
                    imported?.ActionConfigJson ??
                    candidate.ActionConfigJson,

                ExecutionOrder =
                    imported?.ExecutionOrder ??
                    candidate.ExecutionOrder,

                IsActive =
                    imported?.IsActive ?? candidate.IsActive,

                ExistingValue = existingRecord is null
                    ? null
                    : ToExistingValue(existingRecord),

                Errors = rowErrors
            };
        }

        private static SopRuleImportExistingValueResponse ToExistingValue(
            SopRuleImportReadModel rule)
        {
            return new SopRuleImportExistingValueResponse
            {
                Id = rule.Id,
                RuleCode = rule.RuleCode,
                RuleName = rule.RuleName,
                Description = rule.Description,
                TriggerRiskLevel = rule.TriggerRiskLevel,
                PreviousRiskLevel = rule.PreviousRiskLevel,
                AppliesToZoneType = rule.AppliesToZoneType,
                ActionType = rule.ActionType,
                ActionConfigJson = rule.ActionConfigJson,
                ExecutionOrder = rule.ExecutionOrder,
                IsActive = rule.IsActive,
                Version = rule.Version
            };
        }

        private static SopRuleImportCandidate ToCurrentCandidate(
            SopRuleImportReadModel rule)
        {
            return new SopRuleImportCandidate(
                RowNumber: 0,
                RuleCode: rule.RuleCode,
                RuleName: rule.RuleName,
                Description: rule.Description,
                TriggerRiskLevel: rule.TriggerRiskLevel,
                PreviousRiskLevel: rule.PreviousRiskLevel,
                AppliesToZoneType: rule.AppliesToZoneType,
                ActionType: rule.ActionType,
                ActionConfigJson: rule.ActionConfigJson,
                ExecutionOrder: rule.ExecutionOrder,
                IsActive: rule.IsActive);
        }

        private static bool AreEquivalent(
            ValidatedSopRuleImport existing,
            ValidatedSopRuleImport imported)
        {
            return string.Equals(
                       existing.RuleName,
                       imported.RuleName,
                       StringComparison.Ordinal) &&

                   string.Equals(
                       NormalizeDescription(existing.Description),
                       NormalizeDescription(imported.Description),
                       StringComparison.Ordinal) &&

                   existing.TriggerRiskLevel ==
                       imported.TriggerRiskLevel &&

                   existing.PreviousRiskLevel ==
                       imported.PreviousRiskLevel &&

                   existing.AppliesToZoneType ==
                       imported.AppliesToZoneType &&

                   existing.ActionType ==
                       imported.ActionType &&

                   JsonEquivalent(
                       existing.ActionConfigJson,
                       imported.ActionConfigJson) &&

                   existing.ExecutionOrder ==
                       imported.ExecutionOrder &&

                   existing.IsActive ==
                       imported.IsActive;
        }

        /// So sánh nội dung JSON thay vì khoảng trắng hoặc thứ tự thuộc tính
        private static bool JsonEquivalent(
            string first,
            string second)
        {
            var firstNode = JsonNode.Parse(first);
            var secondNode = JsonNode.Parse(second);

            return JsonNode.DeepEquals(
                firstNode,
                secondNode);
        }

        private static string RuleKey(
            ValidatedSopRuleImport rule) =>
            NormalizeRuleCode(rule.RuleCode);

        private static string NormalizeRuleCode(string ruleCode) =>
            ruleCode.Trim().ToUpperInvariant();

        private static string? NormalizeDescription(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();

        private static string? NormalizeDisplayOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim().ToUpperInvariant();

            return normalized is "ANY" or "ALL"
                ? null
                : normalized;
        }

        private static SopRuleImportPreviewResponse AddTopLevelError(
            SopRuleImportPreviewResponse preview,
            string column,
            string message)
        {
            var errors = preview.Errors
                .Append(new SopRuleImportErrorResponse
                {
                    RowNumber = 0,
                    Column = column,
                    Message = message
                })
                .ToList();

            return new SopRuleImportPreviewResponse
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

        private static SopRuleImportErrorResponse ToErrorResponse(
            SopRuleImportValidationError error)
        {
            return new SopRuleImportErrorResponse
            {
                RowNumber = error.RowNumber,
                Column = error.Column,
                Message = error.Message
            };
        }

        private sealed record SopRulePreviewBuildResult(
            SopRuleImportPreviewResponse Preview,
            IReadOnlyList<ValidatedSopRuleImport> ImportedRows);
    }

    /// Kết quả nội bộ trả cho controller sau khi xác nhận import
    public sealed record SopRuleImportExecutionResult(
        bool IsSuccess,
        SopRuleImportPreviewResponse Preview,
        Guid? ImportBatchId,
        SopRulesReadModel? Configuration);
}