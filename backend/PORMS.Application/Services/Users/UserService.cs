using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Exceptions;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Users;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Users
{
    /// Triển khai IUserService: CRUD user, reset password, unlock.
    /// Map User entity → UserDto (không bao giờ leak password hash / refresh token).
    /// Audit events (operation_events) hiện stub — sẽ thêm ở pass riêng.
    public sealed class UserService : IUserService
    {
        private readonly IApplicationDbContext _db;
        private readonly IPasswordHasher _passwordHasher;

        public UserService(IApplicationDbContext db, IPasswordHasher passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        // READS

        public async Task<UserDto?> GetByIdAsync(Guid id)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            return user is null ? null : ToDto(user);
        }

        public async Task<UserDto?> GetCurrentAsync(Guid userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user is null ? null : ToDto(user);
        }

        public async Task<(IReadOnlyList<UserDto> Items, int TotalCount)> GetPagedAsync(
            string? search,
            string? role,
            string? status,
            Guid? assignedPortId,
            int page,
            int pageSize)
        {
            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Email.ToLower().Contains(term) ||
                    u.FullName.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(role)
                && Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
            {
                query = query.Where(u => u.Role == roleEnum);
            }

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<UserStatus>(status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(u => u.Status == statusEnum);
            }

            if (assignedPortId.HasValue)
            {
                query = query.Where(u => u.AssignedPortId == assignedPortId.Value);
            }

            var total = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = users.Select(ToDto).ToList();
            return (items, total);
        }

        // WRITES

        public async Task<CreateUserResult> CreateAsync(CreateUserRequest request, Guid actorUserId)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            // Email unique (query filter loại deleted; nhưng email của user deleted vẫn chiếm chỗ
            // vì UNIQUE constraint ở DB — kiểm tra cả deleted bằng IgnoreQueryFilters).
            var emailExists = await _db.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email == email);
            if (emailExists)
            {
                throw new ConflictException("EMAIL_ALREADY_EXISTS", "Email này đã được sử dụng");
            }

            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            {
                throw new ValidationException($"Role không hợp lệ: {request.Role}");
            }

            // Quy tắc role ↔ port: ADMIN phải null port; CA/OP bắt buộc có port.
            ValidateRolePortRule(role, request.AssignedPortId);

            // Password: dùng cái admin nhập, hoặc generate ngẫu nhiên.
            var generated = string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? GenerateRandomPassword()
                : null;
            var plaintext = generated ?? request.TemporaryPassword!;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = request.FullName.Trim(),
                PasswordHash = _passwordHasher.Hash(plaintext),
                Role = role,
                Status = UserStatus.ACTIVE,
                AssignedPortId = request.AssignedPortId,
                PhoneNumber = request.PhoneNumber,
                FailedLoginCount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = actorUserId
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            // TODO(audit): ghi USER_CREATED event (actorUserId, user.Id) khi làm audit pass.

            return new CreateUserResult
            {
                User = ToDto(user),
                GeneratedPassword = generated
            };
        }

        public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, Guid actorUserId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy user");

            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            {
                throw new ValidationException($"Role không hợp lệ: {request.Role}");
            }
            if (!Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var status))
            {
                throw new ValidationException($"Status không hợp lệ: {request.Status}");
            }

            ValidateRolePortRule(role, request.AssignedPortId);

            user.FullName = request.FullName.Trim();
            user.Role = role;
            user.AssignedPortId = request.AssignedPortId;
            user.PhoneNumber = request.PhoneNumber;
            user.Status = status;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            // INACTIVE/SUSPENDED → revoke refresh token (đẩy user ra khỏi hệ thống).
            if (status != UserStatus.ACTIVE)
            {
                user.RefreshTokenHash = null;
                user.RefreshTokenExpiresAt = null;
            }

            await _db.SaveChangesAsync();
            // TODO(audit): USER_UPDATED; nếu role đổi → USER_ROLE_CHANGED.

            return ToDto(user);
        }

        public async Task<UserDto> UpdateOwnProfileAsync(Guid userId, UpdateOwnProfileRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                       ?? throw new NotFoundException("Không tìm thấy user");

            user.FullName = request.FullName.Trim();
            user.PhoneNumber = request.PhoneNumber;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            return ToDto(user);
        }

        public async Task SoftDeleteAsync(Guid id, Guid actorUserId)
        {
            if (id == actorUserId)
            {
                throw new ValidationException("Không thể tự xóa tài khoản của chính mình");
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy user");

            user.DeletedAt = DateTimeOffset.UtcNow;
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            // TODO(audit): USER_DELETED.
        }

        public async Task<AdminResetPasswordResult> AdminResetPasswordAsync(
            Guid id, AdminResetPasswordRequest request, Guid actorUserId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy user");

            var generated = string.IsNullOrWhiteSpace(request.NewPassword)
                ? GenerateRandomPassword()
                : null;
            var plaintext = generated ?? request.NewPassword!;

            user.PasswordHash = _passwordHasher.Hash(plaintext);
            // Reset password → revoke session + clear lockout.
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            // TODO(audit): PASSWORD_RESET.

            return new AdminResetPasswordResult { GeneratedPassword = generated };
        }

        public async Task UnlockAsync(Guid id, Guid actorUserId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy user");

            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            // TODO(audit): USER_UNLOCKED.
        }

        // HELPERS

        private static void ValidateRolePortRule(UserRole role, Guid? assignedPortId)
        {
            if (role == UserRole.ADMIN && assignedPortId.HasValue)
            {
                throw new ValidationException("ADMIN không được gán assigned_port_id");
            }
            if (role != UserRole.ADMIN && !assignedPortId.HasValue)
            {
                throw new ValidationException($"{role} bắt buộc phải có assigned_port_id");
            }
        }

        private static string GenerateRandomPassword()
        {
            // 16 ký tự an toàn, đủ entropy cho mật khẩu tạm. Admin sẽ đưa cho user đổi sau.
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            var sb = new System.Text.StringBuilder(16);
            foreach (var b in bytes)
            {
                sb.Append(chars[b % chars.Length]);
            }
            return sb.ToString();
        }

        // MAPPING

        private static UserDto ToDto(User u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role.ToString(),
            Status = u.Status.ToString(),
            AssignedPortId = u.AssignedPortId,
            PhoneNumber = u.PhoneNumber,
            LastLoginAt = u.LastLoginAt,
            IsLocked = u.LockedUntil.HasValue && u.LockedUntil.Value > DateTimeOffset.UtcNow,
            LockedUntil = u.LockedUntil,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt,
            CreatedByUserId = u.CreatedByUserId
        };
    }
}
