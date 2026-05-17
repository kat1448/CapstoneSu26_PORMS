using PORMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Domain.Entities
{
    public class User
    {
        /// UUID PK — DB tự sinh qua uuid_generate_v4().
        public Guid Id { get; set; }

        /// Email là username, unique toàn hệ thống.
        public string Email { get; set; } = string.Empty;

        /// Họ tên đầy đủ — hiển thị trên UI và audit log.
        public string FullName { get; set; } = string.Empty;

        /// BCrypt hash với cost=12. KHÔNG bao giờ log, KHÔNG bao giờ return qua API.
        /// DTO phải loại bỏ field này khi mapping ra response.
        public string PasswordHash { get; set; } = string.Empty;

        /// Vai trò — ADMIN / COMPANY_ADMIN / OPERATOR. Default OPERATOR ở DB.
        public UserRole Role { get; set; } = UserRole.OPERATOR;

        /// Trạng thái tài khoản. Default ACTIVE ở DB.
        public UserStatus Status { get; set; } = UserStatus.ACTIVE;

        /// Port phụ trách. NULL = ADMIN (xem tất cả port).
        /// COMPANY_ADMIN và OPERATOR bắt buộc có giá trị này.
        public Guid? AssignedPortId { get; set; }

        /// Số điện thoại — liên lạc khẩn cấp khi CRITICAL alert. Format VN.
        public string? PhoneNumber { get; set; }

        /// Hash của refresh token hiện tại. NULL nếu chưa login hoặc đã logout.
        /// Schema hiện tại chỉ hỗ trợ 1 session/user (flag).
        public string? RefreshTokenHash { get; set; }

        /// Thời điểm refresh token hết hạn. NULL khi không có session active.
        public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

        /// Thời điểm đăng nhập thành công gần nhất — audit và security monitoring.
        public DateTimeOffset? LastLoginAt { get; set; }

        /// Số lần login fail liên tiếp. Reset về 0 khi login thành công.
        /// Khi đạt 5: set LockedUntil = NOW + 15 phút.
        public short FailedLoginCount { get; set; }

        /// Thời điểm hết khóa tạm thời. NULL = không bị khóa.
        /// Kiểm tra lockout: LockedUntil.HasValue &amp;&amp; LockedUntil.Value &gt; DateTimeOffset.UtcNow.
        public DateTimeOffset? LockedUntil { get; set; }

        /// Thời điểm tạo record — DB tự set qua DEFAULT NOW().
        public DateTimeOffset CreatedAt { get; set; }

        /// Thời điểm update cuối — application set khi sửa, hoặc dùng trigger DB.
        public DateTimeOffset UpdatedAt { get; set; }

        /// Soft delete timestamp. NULL = chưa xóa. Mọi query phải filter DeletedAt IS NULL.
        public DateTimeOffset? DeletedAt { get; set; }

        /// User đã tạo tài khoản này. NULL nếu seed data hoặc đăng ký tự động.
        /// Self-reference: User có thể tham chiếu User khác.
        public Guid? CreatedByUserId { get; set; }
    }
}
