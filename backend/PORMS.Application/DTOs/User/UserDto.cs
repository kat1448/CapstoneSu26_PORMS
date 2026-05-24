using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.User
{
    /// Đại diện user trong tất cả response của API.
    /// KHÔNG bao gồm: PasswordHash, RefreshTokenHash, RefreshTokenExpiresAt
    /// (các field nội bộ — không leak qua API).
    public sealed class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? AssignedPortId { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
        public bool IsLocked { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Guid? CreatedByUserId { get; set; }
    }
}
