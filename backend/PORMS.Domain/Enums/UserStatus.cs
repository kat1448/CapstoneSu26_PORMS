using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Domain.Enums
{
    /// Trạng thái tài khoản — ánh xạ tới PostgreSQL enum operational.user_status_enum.
    /// Lưu ý: trạng thái khóa tạm thời do failed login KHÔNG dùng enum này,
    /// mà dùng cột locked_until (TIMESTAMPTZ). Kiểm tra lockout:
    /// user.LockedUntil.HasValue &amp;&amp; user.LockedUntil.Value &gt; DateTimeOffset.UtcNow
    public enum UserStatus
    {
        /// Hoạt động bình thường, có thể đăng nhập.
        ACTIVE,

        /// Bị Admin vô hiệu hóa tạm thời (có thể re-activate).
        INACTIVE,

        /// Bị đình chỉ vĩnh viễn hoặc kỷ luật bởi Admin — cần Admin can thiệp để khôi phục.
        SUSPENDED
    }
}
