using PORMS.Application.Common.Exceptions;
using PORMS.Application.DTOs.Auths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Auths
{
    /// Quản lý session của người dùng đang đăng nhập:
    /// đăng nhập, refresh token, logout, đổi password của chính mình.
    /// Các thao tác admin trên tài khoản khác (reset, unlock) thuộc IUserService.
    public interface IAuthService
    {
        /// Đăng nhập bằng email + password
        /// Trả LoginResponse (access token + user info). Refresh token được trả riêng
        /// để controller set vào httpOnly cookie — KHÔNG nằm trong LoginResponse body.
        Task<LoginResult> LoginAsync(LoginRequest request, string? deviceInfo);

        /// Đổi access token mới từ refresh token (đọc từ cookie).
        /// Validate token khớp hash trong DB và chưa expired.
        Task<LoginResult> RefreshTokenAsync(string rawRefreshToken);

        /// Đăng xuất: xóa RefreshTokenHash và RefreshTokenExpiresAt của user.
        Task LogoutAsync(Guid userId);

        /// User tự đổi password (US-005). Verify CurrentPassword trước khi update.
        /// Sau đổi: revoke refresh token (buộc đăng nhập lại).
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

        /// Kết quả nội bộ của LoginAsync/RefreshTokenAsync.
        /// Response: phần trả về body (access token + user).
        /// RawRefreshToken: token thô để controller set vào httpOnly cookie — KHÔNG serialize ra body
        public sealed class LoginResult
        {
            public LoginResponse Response { get; set; } = new();
            public string RawRefreshToken { get; set; } = string.Empty;
            public DateTimeOffset RefreshTokenExpiresAt { get; set; }
        }
    }
}
