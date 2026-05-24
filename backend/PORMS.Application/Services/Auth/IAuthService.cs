using PORMS.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Auth
{
    /// Quản lý session của người dùng đang đăng nhập:
    /// đăng nhập, refresh token, logout, đổi password của chính mình.
    /// Các thao tác admin trên tài khoản khác (reset, unlock) thuộc IUserService.
    public interface IAuthService
    {
        /// Đăng nhập bằng email + password (US-001).
        /// Side effects khi thành công: reset FailedLoginCount, set LastLoginAt,
        /// generate refresh token mới (hash lưu DB).
        /// Side effects khi thất bại: tăng FailedLoginCount;
        /// nếu đạt 5 thì set LockedUntil = NOW + 15 phút.
        /// Throw nếu account INACTIVE/SUSPENDED hoặc đang LOCKED.
        Task<LoginResponse> LoginAsync(LoginRequest request);

        /// Đổi access token mới khi token cũ hết hạn (US-001 refresh flow).
        /// Validate RefreshToken khớp hash trong DB và chưa expired.
        /// Rotate: cấp refresh token mới, invalidate cái cũ.
        Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);

        /// Đăng xuất user hiện tại: xóa RefreshTokenHash và RefreshTokenExpiresAt.
        /// Access token vẫn còn hiệu lực đến khi expire (JWT stateless)
        Task LogoutAsync(Guid userId);

        /// User tự đổi password (US-005). Verify CurrentPassword trước khi update.
        /// Sau đổi password: revoke refresh token (force re-login tất cả thiết bị).
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
