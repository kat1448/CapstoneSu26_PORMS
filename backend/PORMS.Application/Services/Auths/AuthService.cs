using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Exceptions;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Auths;
using PORMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Auths
{
    /// Triển khai IAuthService: login (lockout sau 5 lần sai), refresh token rotation,
    /// logout, đổi password. Refresh token lưu dạng SHA-256 hash trong users.refresh_token_hash.
    public sealed class AuthService : IAuthService
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private const int RefreshTokenDays = 7;

        private readonly IApplicationDbContext _db;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwt;

        public AuthService(
            IApplicationDbContext db,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwt)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _jwt = jwt;
        }

        public async Task<IAuthService.LoginResult> LoginAsync(LoginRequest request, string? deviceInfo)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Email không tồn tại → cùng lỗi generic như sai password (chống enumeration).
            if (user is null)
            {
                throw new InvalidCredentialsException();
            }

            // Đang bị khóa tạm thời?
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTimeOffset.UtcNow)
            {
                throw new AccountLockedException();
            }

            // Tài khoản phải ACTIVE.
            if (user.Status != UserStatus.ACTIVE)
            {
                throw new AccountNotActiveException();
            }

            // Verify password.
            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= MaxFailedAttempts)
                {
                    user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                }
                user.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                throw new InvalidCredentialsException();
            }

            // Thành công: reset lockout, set last login.
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTimeOffset.UtcNow;

            var result = await IssueTokensAsync(user);
            await _db.SaveChangesAsync();
            return result;
        }

        public async Task<IAuthService.LoginResult> RefreshTokenAsync(string rawRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                throw new InvalidRefreshTokenException();
            }

            var hash = _jwt.HashRefreshToken(rawRefreshToken);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash);

            if (user is null
                || !user.RefreshTokenExpiresAt.HasValue
                || user.RefreshTokenExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                throw new InvalidRefreshTokenException();
            }

            // Tài khoản vẫn phải ACTIVE để refresh.
            if (user.Status != UserStatus.ACTIVE)
            {
                throw new AccountNotActiveException();
            }

            // Rotate: cấp token mới, hash cũ bị thay thế.
            var result = await IssueTokensAsync(user);
            await _db.SaveChangesAsync();
            return result;
        }

        public async Task LogoutAsync(Guid userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return; // Idempotent: logout của user không tồn tại = no-op.
            }

            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                // User đã đăng nhập nhưng không tìm thấy → coi như credentials lỗi.
                throw new InvalidCredentialsException();
            }

            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new InvalidCurrentPasswordException();
            }

            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            // Đổi password → revoke refresh token (buộc đăng nhập lại).
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// Sinh access + refresh token cho user, lưu hash refresh token vào entity.
        /// KHÔNG gọi SaveChanges (để caller quyết định khi nào commit).
        private Task<IAuthService.LoginResult> IssueTokensAsync(Domain.Entities.User user)
        {
            var (accessToken, expiresAt) = _jwt.GenerateAccessToken(user);
            var rawRefresh = _jwt.GenerateRefreshToken();
            var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);

            user.RefreshTokenHash = _jwt.HashRefreshToken(rawRefresh);
            user.RefreshTokenExpiresAt = refreshExpiresAt;

            var response = new LoginResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                User = new AuthenticatedUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role.ToString(),
                    AssignedPortId = user.AssignedPortId
                }
            };

            return Task.FromResult(new IAuthService.LoginResult
            {
                Response = response,
                RawRefreshToken = rawRefresh,
                RefreshTokenExpiresAt = refreshExpiresAt
            });
        }
    }
}
