using PORMS.Application.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Users
{
    /// Quản lý vòng đời tài khoản người dùng (Admin perspective + self-profile).
    /// Mọi method thay đổi dữ liệu yêu cầu actorUserId để ghi audit log.
    public interface IUserService
    {
        /// ADMIN tạo tài khoản mới (US-008). Validate email unique, role hợp lệ,
        /// AssignedPortId tương ứng với role (NULL chỉ cho ADMIN).
        /// Hash password (BCrypt cost=12). Ghi USER_CREATED event.
        /// Nếu request.TemporaryPassword == null, service generate password ngẫu nhiên
        /// và set vào CreateUserResult.GeneratedPassword (chỉ trả về lần này)
        Task<CreateUserResult> CreateAsync(CreateUserRequest request, Guid actorUserId);

        /// Lấy 1 user theo Id. Trả null nếu không tồn tại hoặc đã soft-delete
        Task<UserDto?> GetByIdAsync(Guid id);

        /// Danh sách user có phân trang + filter (US-009).
        /// Filter: search (email/full_name), role, status, assignedPortId.
        /// Trả về tuple gồm list users và tổng số bản ghi (để FE tính số trang)
        Task<(IReadOnlyList<UserDto> Items, int TotalCount)> GetPagedAsync(
            string? search,
            string? role,
            string? status,
            Guid? assignedPortId,
            int page,
            int pageSize);

        /// Lấy profile của user đang đăng nhập (GET /api/users/me — US-010).
        Task<UserDto?> GetCurrentAsync(Guid userId);

        /// ADMIN sửa thông tin user khác (PUT /api/users/{id} — US-009).
        /// Ghi USER_UPDATED event; nếu Role thay đổi, ghi thêm USER_ROLE_CHANGED.
        /// Đổi Status sang INACTIVE/SUSPENDED revoke tất cả refresh tokens.
        Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, Guid actorUserId);

        /// User tự sửa profile của mình (PUT /api/users/me — US-010).
        /// Chỉ FullName và PhoneNumber được phép sửa.
        Task<UserDto> UpdateOwnProfileAsync(Guid userId, UpdateOwnProfileRequest request);

        /// Soft delete: set DeletedAt = NOW. Revoke refresh tokens.
        /// User không thể tự xóa mình — service validate id != actorUserId.
        Task SoftDeleteAsync(Guid id, Guid actorUserId);

        /// ADMIN reset password user khác (US-012). Hash password mới, revoke refresh tokens,
        /// clear lockout (FailedLoginCount=0, LockedUntil=NULL).
        /// Ghi PASSWORD_RESET event. Trả về password đã sinh ngẫu nhiên (nếu request.NewPassword null).
        Task<AdminResetPasswordResult> AdminResetPasswordAsync(
            Guid id,
            AdminResetPasswordRequest request,
            Guid actorUserId);

        /// ADMIN mở khóa tài khoản bị lock (US-013).
        /// Reset FailedLoginCount=0, LockedUntil=NULL. Ghi USER_UNLOCKED event.
        Task UnlockAsync(Guid id, Guid actorUserId);
    }

    /// Kết quả CreateAsync. GeneratedPassword chỉ có giá trị nếu request.TemporaryPassword null
    /// (admin để service tự sinh). Đây là LẦN DUY NHẤT password lộ ra ngoài plaintext.
    public sealed class CreateUserResult
    {
        public UserDto User { get; set; } = new();
        public string? GeneratedPassword { get; set; }
    }

    /// Kết quả AdminResetPasswordAsync — analog với CreateUserResult.
    public sealed class AdminResetPasswordResult
    {
        public string? GeneratedPassword { get; set; }
    }
}
