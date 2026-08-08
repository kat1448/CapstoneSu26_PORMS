using System.Text.Json;
using System.Text.RegularExpressions;

namespace PORMS.API.Services
{
    /// Kiểm tra và chuẩn hóa từng dòng SOP trước khi preview hoặc import
    /// Validator này không truy cập database và không thay đổi SOP hiện tại
    public sealed class SopRuleImportValidator
    {
        private const int MaximumDescriptionLength = 2000;
        private const int MaximumActionConfigLength = 4000;

        private static readonly HashSet<string> AllowedRiskLevels =
            ["LOW", "MEDIUM", "HIGH", "CRITICAL"];

        private static readonly HashSet<string> AllowedZoneTypes =
            ["DOCK", "YARD", "GATE", "WAREHOUSE"];

        private static readonly HashSet<string> AllowedActionTypes =
        [
            "CREATE_TASK",
            "SEND_ALERT",
            "RESTRICT_ZONE",
            "UNRESTRICT_ZONE",
            "SET_NORMAL_MODE",
            "SET_LIMITED_MODE",
            "STOP_OPERATIONS"
        ];

        private static readonly HashSet<string> AllowedOperationModes =
            ["NORMAL", "LIMITED", "STOP"];

        public SopRuleImportValidationResult ValidateRows(
            IReadOnlyList<SopRuleImportCandidate> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);

            var validRows = new List<ValidatedSopRuleImport>();
            var errors = new List<SopRuleImportValidationError>();
            var seenRuleCodes =
                new Dictionary<string, int>(StringComparer.Ordinal);

            if (candidates.Count == 0)
            {
                errors.Add(new(
                    0,
                    "File",
                    "File không chứa dòng quy tắc SOP nào."));

                return new(validRows, errors);
            }

            foreach (var candidate in candidates)
            {
                var errorCountBeforeRow = errors.Count;

                var ruleCode = NormalizeRuleCode(candidate, errors);
                var ruleName = NormalizeRuleName(candidate, errors);
                var description = NormalizeDescription(candidate, errors);

                var triggerRiskLevel = NormalizeRequiredEnum(
                    candidate.RowNumber,
                    candidate.TriggerRiskLevel,
                    "TriggerRiskLevel",
                    AllowedRiskLevels,
                    "LOW, MEDIUM, HIGH hoặc CRITICAL",
                    errors);

                var previousRiskLevel = NormalizeOptionalEnum(
                    candidate.RowNumber,
                    candidate.PreviousRiskLevel,
                    "PreviousRiskLevel",
                    AllowedRiskLevels,
                    "ANY, LOW, MEDIUM, HIGH hoặc CRITICAL",
                    errors);

                var appliesToZoneType = NormalizeOptionalEnum(
                    candidate.RowNumber,
                    candidate.AppliesToZoneType,
                    "AppliesToZoneType",
                    AllowedZoneTypes,
                    "ALL, DOCK, YARD, GATE hoặc WAREHOUSE",
                    errors);

                var actionType = NormalizeRequiredEnum(
                    candidate.RowNumber,
                    candidate.ActionType,
                    "ActionType",
                    AllowedActionTypes,
                    string.Join(", ", AllowedActionTypes),
                    errors);

                var actionConfigJson = ValidateAndNormalizeActionConfig(
                    candidate,
                    actionType,
                    ruleName,
                    triggerRiskLevel,
                    errors);

                var executionOrder = candidate.ExecutionOrder
                    ?? SopRuleAutomationPolicy.GetExecutionOrder(actionType);

                if (executionOrder is < 0 or > short.MaxValue)
                {
                    errors.Add(new(
                        candidate.RowNumber,
                        "ExecutionOrder",
                        $"ExecutionOrder phải nằm trong khoảng 0 đến {short.MaxValue}."));
                }

                if (candidate.IsActive is null)
                {
                    errors.Add(new(
                        candidate.RowNumber,
                        "IsActive",
                        "IsActive phải là TRUE hoặc FALSE."));
                }

                // RuleCode là khóa nghiệp vụ dùng để xác định CREATE hoặc UPDATE
                if (ruleCode is not null)
                {
                    if (seenRuleCodes.TryGetValue(ruleCode, out var firstRow))
                    {
                        errors.Add(new(
                            candidate.RowNumber,
                            "RuleCode",
                            $"RuleCode {ruleCode} đã xuất hiện tại dòng {firstRow}."));
                    }
                    else
                    {
                        seenRuleCodes[ruleCode] = candidate.RowNumber;
                    }
                }

                // Chỉ đưa dòng vào danh sách hợp lệ khi dòng đó không có lỗi
                if (errors.Count == errorCountBeforeRow)
                {
                    validRows.Add(new ValidatedSopRuleImport(
                        candidate.RowNumber,
                        ruleCode!,
                        ruleName!,
                        description,
                        triggerRiskLevel!,
                        previousRiskLevel,
                        appliesToZoneType,
                        actionType!,
                        actionConfigJson!,
                        (short)executionOrder,
                        candidate.IsActive!.Value));
                }
            }

            return new(validRows, errors);
        }

        private static string? NormalizeRuleCode(
            SopRuleImportCandidate candidate,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(candidate.RuleCode))
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "RuleCode",
                    "RuleCode là bắt buộc."));

                return null;
            }

            var normalized = candidate.RuleCode
                .Trim()
                .ToUpperInvariant();

            if (normalized.Length > 80)
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "RuleCode",
                    "RuleCode không được vượt quá 80 ký tự."));
            }
            else if (!Regex.IsMatch(
                         normalized,
                         "^[A-Z0-9][A-Z0-9_-]*$",
                         RegexOptions.CultureInvariant))
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "RuleCode",
                    "RuleCode chỉ được chứa chữ in hoa, số, dấu gạch ngang hoặc gạch dưới."));
            }

            return normalized;
        }

        private static string? NormalizeRuleName(
            SopRuleImportCandidate candidate,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(candidate.RuleName))
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "RuleName",
                    "RuleName là bắt buộc."));

                return null;
            }

            var normalized = candidate.RuleName.Trim();

            if (normalized.Length > 255)
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "RuleName",
                    "RuleName không được vượt quá 255 ký tự."));
            }

            return normalized;
        }

        private static string? NormalizeDescription(
            SopRuleImportCandidate candidate,
            ICollection<SopRuleImportValidationError> errors)
        {
            var normalized = string.IsNullOrWhiteSpace(candidate.Description)
                ? null
                : candidate.Description.Trim();

            if (normalized?.Length > MaximumDescriptionLength)
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "Description",
                    $"Description không được vượt quá {MaximumDescriptionLength} ký tự."));
            }

            return normalized;
        }

        private static string? NormalizeRequiredEnum(
            int rowNumber,
            string? value,
            string column,
            HashSet<string> allowedValues,
            string allowedDescription,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new(
                    rowNumber,
                    column,
                    $"{column} là bắt buộc."));

                return null;
            }

            var normalized = value.Trim().ToUpperInvariant();

            if (!allowedValues.Contains(normalized))
            {
                errors.Add(new(
                    rowNumber,
                    column,
                    $"{column} chỉ chấp nhận {allowedDescription}."));

                return null;
            }

            return normalized;
        }

        private static string? NormalizeOptionalEnum(
            int rowNumber,
            string? value,
            string column,
            HashSet<string> allowedValues,
            string allowedDescription,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim().ToUpperInvariant();

            // ANY và ALL đều được lưu thành NULL trong database
            if (normalized is "ANY" or "ALL")
                return null;

            if (!allowedValues.Contains(normalized))
            {
                errors.Add(new(
                    rowNumber,
                    column,
                    $"{column} chỉ chấp nhận {allowedDescription}."));

                return null;
            }

            return normalized;
        }

        private static string? ValidateAndNormalizeActionConfig(
            SopRuleImportCandidate candidate,
            string? actionType,
            string? ruleName,
            string? triggerRiskLevel,
            ICollection<SopRuleImportValidationError> errors)
        {
            var json = string.IsNullOrWhiteSpace(candidate.ActionConfigJson)
                ? SopRuleAutomationPolicy.CreateActionConfig(
                    actionType,
                    ruleName,
                    triggerRiskLevel)
                : candidate.ActionConfigJson.Trim();

            if (json.Length > MaximumActionConfigLength)
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "ActionConfigJson",
                    $"ActionConfigJson không được vượt quá {MaximumActionConfigLength} ký tự."));

                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(
                    json,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 16
                    });

                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new(
                        candidate.RowNumber,
                        "ActionConfigJson",
                        "ActionConfigJson phải là một JSON object, ví dụ {\"mode\":\"STOP\"}."));

                    return null;
                }

                ValidateDuplicateJsonProperties(
                    candidate.RowNumber,
                    root,
                    errors);

                var hasMode = root.TryGetProperty("mode", out _);
                var hasTitle = root.TryGetProperty("title", out _);
                var hasPriority = root.TryGetProperty("priority", out _);

                var mode = ReadOptionalStringProperty(
                    candidate.RowNumber,
                    root,
                    "mode",
                    20,
                    errors);

                var title = ReadOptionalStringProperty(
                    candidate.RowNumber,
                    root,
                    "title",
                    255,
                    errors);

                var priority = ReadOptionalStringProperty(
                    candidate.RowNumber,
                    root,
                    "priority",
                    20,
                    errors);

                if (mode is not null &&
                    !AllowedOperationModes.Contains(mode.ToUpperInvariant()))
                {
                    errors.Add(new(
                        candidate.RowNumber,
                        "ActionConfigJson",
                        "Thuộc tính mode chỉ chấp nhận NORMAL, LIMITED hoặc STOP."));
                }

                if (priority is not null &&
                    !AllowedRiskLevels.Contains(priority.ToUpperInvariant()))
                {
                    errors.Add(new(
                        candidate.RowNumber,
                        "ActionConfigJson",
                        "Thuộc tính priority chỉ chấp nhận LOW, MEDIUM, HIGH hoặc CRITICAL."));
                }

                ValidateOptionalBooleanProperty(
                    candidate.RowNumber,
                    root,
                    "createTask",
                    errors);

                ValidateOptionalBooleanProperty(
                    candidate.RowNumber,
                    root,
                    "sendAlert",
                    errors);

                ValidateOptionalBooleanProperty(
                    candidate.RowNumber,
                    root,
                    "requiresInspection",
                    errors);

                ValidateActionRequirements(
                    candidate.RowNumber,
                    actionType,
                    hasMode,
                    mode,
                    hasTitle,
                    title,
                    hasPriority,
                    priority,
                    errors);

                // Lưu JSON ở dạng gọn để so sánh và ghi database ổn định
                return JsonSerializer.Serialize(root);
            }
            catch (JsonException)
            {
                errors.Add(new(
                    candidate.RowNumber,
                    "ActionConfigJson",
                    "ActionConfigJson không phải JSON hợp lệ."));

                return null;
            }
        }

        private static void ValidateDuplicateJsonProperties(
            int rowNumber,
            JsonElement root,
            ICollection<SopRuleImportValidationError> errors)
        {
            var propertyNames =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in root.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name))
                {
                    errors.Add(new(
                        rowNumber,
                        "ActionConfigJson",
                        $"ActionConfigJson chứa thuộc tính trùng lặp: {property.Name}."));
                }
            }
        }

        private static string? ReadOptionalStringProperty(
            int rowNumber,
            JsonElement root,
            string propertyName,
            int maximumLength,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            if (property.ValueKind != JsonValueKind.String)
            {
                errors.Add(new(
                    rowNumber,
                    "ActionConfigJson",
                    $"Thuộc tính {propertyName} phải là chuỗi."));

                return null;
            }

            var value = property.GetString()?.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new(
                    rowNumber,
                    "ActionConfigJson",
                    $"Thuộc tính {propertyName} không được để trống."));

                return null;
            }

            if (value.Length > maximumLength)
            {
                errors.Add(new(
                    rowNumber,
                    "ActionConfigJson",
                    $"Thuộc tính {propertyName} không được vượt quá {maximumLength} ký tự."));
            }

            return value;
        }

        private static void ValidateOptionalBooleanProperty(
            int rowNumber,
            JsonElement root,
            string propertyName,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return;

            if (property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add(new(
                    rowNumber,
                    "ActionConfigJson",
                    $"Thuộc tính {propertyName} phải là true hoặc false."));
            }
        }

        private static void ValidateActionRequirements(
            int rowNumber,
            string? actionType,
            bool hasMode,
            string? mode,
            bool hasTitle,
            string? title,
            bool hasPriority,
            string? priority,
            ICollection<SopRuleImportValidationError> errors)
        {
            if (actionType is null)
                return;

            var expectedMode = actionType switch
            {
                "SET_NORMAL_MODE" => "NORMAL",
                "SET_LIMITED_MODE" => "LIMITED",
                "STOP_OPERATIONS" => "STOP",
                _ => null
            };

            if (expectedMode is not null)
            {
                if (!hasMode)
                {
                    errors.Add(new(
                        rowNumber,
                        "ActionConfigJson",
                        $"Action {actionType} yêu cầu thuộc tính \"mode\":\"{expectedMode}\"."));
                }
                else if (mode is not null &&
                         !string.Equals(
                             mode,
                             expectedMode,
                             StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new(
                        rowNumber,
                        "ActionConfigJson",
                        $"Action {actionType} phải sử dụng mode {expectedMode}."));
                }
            }

            if (actionType == "CREATE_TASK")
            {
                if (!hasTitle || string.IsNullOrWhiteSpace(title))
                {
                    errors.Add(new(
                        rowNumber,
                        "ActionConfigJson",
                        "Action CREATE_TASK yêu cầu thuộc tính title."));
                }

                if (!hasPriority || string.IsNullOrWhiteSpace(priority))
                {
                    errors.Add(new(
                        rowNumber,
                        "ActionConfigJson",
                        "Action CREATE_TASK yêu cầu thuộc tính priority."));
                }
            }
        }
    }

    /// Dữ liệu thô được đọc từ một dòng Excel
    public sealed record SopRuleImportCandidate(
        int RowNumber,
        string? RuleCode,
        string? RuleName,
        string? Description,
        string? TriggerRiskLevel,
        string? PreviousRiskLevel,
        string? AppliesToZoneType,
        string? ActionType,
        string? ActionConfigJson,
        int? ExecutionOrder,
        bool? IsActive);

    /// Dữ liệu đã được chuẩn hóa và có thể dùng cho preview/import
    public sealed record ValidatedSopRuleImport(
        int RowNumber,
        string RuleCode,
        string RuleName,
        string? Description,
        string TriggerRiskLevel,
        string? PreviousRiskLevel,
        string? AppliesToZoneType,
        string ActionType,
        string ActionConfigJson,
        short ExecutionOrder,
        bool IsActive);

    public sealed record SopRuleImportValidationError(
        int RowNumber,
        string Column,
        string Message);

    public sealed record SopRuleImportValidationResult(
        IReadOnlyList<ValidatedSopRuleImport> ValidRows,
        IReadOnlyList<SopRuleImportValidationError> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }
}
