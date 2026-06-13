using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Users
{
    /// Body cho PUT /api/users/me — user tự sửa profile của mình.
    /// Chỉ FullName và PhoneNumber được phép sửa.
    /// Đổi Email, Role, AssignedPortId yêu cầu Admin thao tác qua /api/users/{id}.
    public sealed class UpdateOwnProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }
}
