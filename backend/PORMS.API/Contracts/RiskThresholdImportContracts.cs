using Microsoft.AspNetCore.Http;

namespace PORMS.API.Contracts
{
    /// Dữ liệu multipart/form-data gửi lên khi xem trước hoặc nhập file Excel
    /// ChangeReason chỉ bắt buộc đối với thao tác nhập chính thức
    public sealed class RiskThresholdImportRequest
    {
        public IFormFile? File { get; init; }

        public string? ChangeReason { get; init; }
    }

    /// Kết quả phân tích file trước khi ghi dữ liệu vào database
    /// Endpoint preview chỉ trả về đối tượng này và không thay đổi dữ liệu.
    public sealed class RiskThresholdImportPreviewResponse
    {
        public required string FileName { get; init; }

        public required int TotalRows { get; init; }

        public required int ValidRows { get; init; }

        public required int InvalidRows { get; init; }

        public required int CreateCount { get; init; }

        public required int UpdateCount { get; init; }

        public required int UnchangedCount { get; init; }

        public required bool CanImport { get; init; }

        public required IReadOnlyList<RiskThresholdImportRowResponse> Rows { get; init; }

        // Lỗi cấp file hoặc cấu hình, không thuộc riêng một dòng dữ liệu.
        public required IReadOnlyList<RiskThresholdImportErrorResponse> Errors { get; init; }
    }

    /// Kết quả phân tích của một dòng trong file Excel
    /// Action nhận một trong bốn giá trị:
    /// CREATE, UPDATE, UNCHANGED hoặc INVALID
    public sealed class RiskThresholdImportRowResponse
    {
        public required int RowNumber { get; init; }

        public required string Action { get; init; }

        public string? Factor { get; init; }

        public string? RiskLevel { get; init; }

        public string? ComparisonOperator { get; init; }

        public decimal? ThresholdValue { get; init; }

        public string? Unit { get; init; }

        public string? Description { get; init; }

        public bool? IsEnabled { get; init; }

        // Chứa dữ liệu hiện tại khi dòng Excel sẽ cập nhật một threshold đã tồn tại
        public RiskThresholdImportExistingValueResponse? ExistingValue { get; init; }

        public required IReadOnlyList<RiskThresholdImportErrorResponse> Errors { get; init; }
    }

    /// Giá trị hiện có trong database để người dùng so sánh trước khi xác nhận
    public sealed class RiskThresholdImportExistingValueResponse
    {
        public required string ComparisonOperator { get; init; }

        public required decimal ThresholdValue { get; init; }

        public required string Unit { get; init; }

        public string? Description { get; init; }

        public required bool IsEnabled { get; init; }
    }

    /// Lỗi cụ thể của một ô hoặc một quy tắc nghiệp vụ trong file Excel
    public sealed class RiskThresholdImportErrorResponse
    {
        public required int RowNumber { get; init; }

        public required string Column { get; init; }

        public required string Message { get; init; }
    }

    /// Kết quả sau khi toàn bộ file đã được kiểm tra và lưu thành công
    public sealed class RiskThresholdImportResponse
    {
        public required string FileName { get; init; }

        public required int CreatedCount { get; init; }

        public required int UpdatedCount { get; init; }

        public required int UnchangedCount { get; init; }

        // Trả lại cấu hình mới nhất để frontend không cần gọi thêm một API GET
        public required RiskConfigResponse Configuration { get; init; }
    }

}
