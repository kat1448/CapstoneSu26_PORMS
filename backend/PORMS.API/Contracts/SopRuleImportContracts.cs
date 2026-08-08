using Microsoft.AspNetCore.Http;

namespace PORMS.API.Contracts
{
    /// Dữ liệu multipart/form-data dùng cho preview và import chính thức
    /// ChangeReason chỉ bắt buộc khi xác nhận import
    public sealed class SopRuleImportRequest
    {
        public IFormFile? File { get; init; }

        public string? ChangeReason { get; init; }
    }

    /// Kết quả kiểm tra toàn bộ file trước khi ghi database
    public sealed class SopRuleImportPreviewResponse
    {
        public required string FileName { get; init; }

        public required int TotalRows { get; init; }

        public required int ValidRows { get; init; }

        public required int InvalidRows { get; init; }

        public required int CreateCount { get; init; }

        public required int UpdateCount { get; init; }

        public required int UnchangedCount { get; init; }

        public required bool CanImport { get; init; }

        public required IReadOnlyList<SopRuleImportRowResponse> Rows { get; init; }

        // Lỗi cấp file hoặc request, không thuộc riêng một dòng Excel
        public required IReadOnlyList<SopRuleImportErrorResponse> Errors { get; init; }
    }

    /// Kết quả kiểm tra của một dòng SOP trong Excel
    /// Action: CREATE, UPDATE, UNCHANGED hoặc INVALID
    public sealed class SopRuleImportRowResponse
    {
        public required int RowNumber { get; init; }

        public required string Action { get; init; }

        public string? RuleCode { get; init; }

        public string? RuleName { get; init; }

        public string? Description { get; init; }

        public string? TriggerRiskLevel { get; init; }

        public string? PreviousRiskLevel { get; init; }

        public string? AppliesToZoneType { get; init; }

        public string? ActionType { get; init; }

        public string? ActionConfigJson { get; init; }

        public int? ExecutionOrder { get; init; }

        public bool? IsActive { get; init; }

        // Có giá trị khi RuleCode đã tồn tại trong database
        public SopRuleImportExistingValueResponse? ExistingValue { get; init; }

        public required IReadOnlyList<SopRuleImportErrorResponse> Errors { get; init; }
    }

    /// Dữ liệu hiện tại để frontend so sánh với dòng Excel
    public sealed class SopRuleImportExistingValueResponse
    {
        public required Guid Id { get; init; }

        public required string RuleCode { get; init; }

        public required string RuleName { get; init; }

        public string? Description { get; init; }

        public required string TriggerRiskLevel { get; init; }

        public string? PreviousRiskLevel { get; init; }

        public string? AppliesToZoneType { get; init; }

        public required string ActionType { get; init; }

        public required string ActionConfigJson { get; init; }

        public required short ExecutionOrder { get; init; }

        public required bool IsActive { get; init; }

        public required int Version { get; init; }
    }

    /// Lỗi cụ thể của một ô hoặc quy tắc nghiệp vụ
    public sealed class SopRuleImportErrorResponse
    {
        public required int RowNumber { get; init; }

        public required string Column { get; init; }

        public required string Message { get; init; }
    }

    /// Kết quả sau khi import thành công
    public sealed class SopRuleImportResponse
    {
        public required Guid ImportBatchId { get; init; }

        public required string FileName { get; init; }

        public required int CreatedCount { get; init; }

        public required int UpdatedCount { get; init; }

        public required int UnchangedCount { get; init; }

        // Trả lại danh sách mới nhất để frontend không cần gọi GET lần nữa
        public required SopRulesResponse Configuration { get; init; }
    }
}
