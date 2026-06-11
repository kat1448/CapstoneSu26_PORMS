namespace PORMS.API.Common
{
    /// Tên các custom claims trong JWT — single source of truth.
    /// AuthService (Sprint 2) khi generate token PHẢI dùng đúng các hằng này;
    /// HttpContextExtensions.IsAuthorizedForPort đọc lại bằng cùng tên.
    /// Sai tên claim → authorization fail âm thầm, rất khó debug.
    public static class ClaimNames
    {
        /// User ID (Guid). Dùng "user_id" cho rõ ràng thay vì "sub".
        public const string UserId = "user_id";

        /// Role: ADMIN / COMPANY_ADMIN / OPERATOR.
        public const string Role = "role";

        /// Port được phân công (Guid). Vắng mặt hoặc rỗng = ADMIN (toàn quyền).
        public const string AssignedPortId = "assigned_port_id";
    }
}
