using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Users
{
    /// Body cho PUT /api/users/{id} (ADMIN only).
    /// Không cho phép sửa Email (immutable sau khi tạo).
    /// Không cho phép sửa Password qua endpoint này — dùng POST /api/users/{id}/reset-password.
    /// Đổi Role ghi audit log USER_ROLE_CHANGED.
    public sealed class UpdateUserRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? AssignedPortId { get; set; }
        public string? PhoneNumber { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
