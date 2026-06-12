using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Auths
{
    /// Response của POST /api/auth/login khi thành công.
    /// AccessToken: JWT, TTL 15 phút.
    /// RefreshToken: opaque string, TTL 7 ngày, lưu hash trong DB.
    /// User: thông tin rút gọn để FE hiển thị + lưu vào local state.
    public sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public AuthenticatedUserDto User { get; set; } = new();
    }

    /// Thông tin user trả về trong LoginResponse — KHÔNG bao gồm password hash,
    /// refresh token, hoặc bất kỳ field nội bộ nào.
    public sealed class AuthenticatedUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? AssignedPortId { get; set; }
    }
}
