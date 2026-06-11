using PORMS.API.Common;
using PORMS.Domain.Enums;

namespace PORMS.API.Extensions
{
    /// Extension methods trên HttpContext cho authorization checks.
    public static class HttpContextExtensions
    {
        /// Kiểm tra user hiện tại có quyền thao tác trên port chỉ định không (US-007 data isolation).
        /// Quy tắc:
        ///   - Chưa authenticated       → false
        ///   - Role ADMIN               → true (xem tất cả port)
        ///   - AssignedPortId == portId → true
        ///   - còn lại                  → false
        ///
        /// Lưu ý Sprint 1: JwtMiddleware hiện là stub nên claims chưa được populate;
        /// method viết đúng nhưng chỉ test đầy đủ được ở Sprint 2 khi login sinh token thật.
        public static bool IsAuthorizedForPort(this HttpContext context, Guid portId)
        {
            var user = context.User;

            // Chưa đăng nhập hoặc token không hợp lệ.
            if (user?.Identity is null || !user.Identity.IsAuthenticated)
            {
                return false;
            }

            // ADMIN: toàn quyền mọi port.
            var role = user.FindFirst(ClaimNames.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.ADMIN), StringComparison.Ordinal))
            {
                return true;
            }

            // COMPANY_ADMIN / OPERATOR: chỉ port được phân công.
            var assignedPortClaim = user.FindFirst(ClaimNames.AssignedPortId)?.Value;
            if (Guid.TryParse(assignedPortClaim, out var assignedPortId))
            {
                return assignedPortId == portId;
            }

            // Không có assigned_port_id hợp lệ và không phải ADMIN → từ chối.
            return false;
        }
    }
}
