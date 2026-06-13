using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Users
{
    /// Body cho POST /api/users (ADMIN only).
    /// Tạo user mới với role và assigned_port_id chỉ định.
    /// COMPANY_ADMIN và OPERATOR bắt buộc có AssignedPortId.
    /// ADMIN phải có AssignedPortId = null.
    /// Service validate các ràng buộc này và trả 400 nếu sai.
    public sealed class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? AssignedPortId { get; set; }
        public string? PhoneNumber { get; set; }

        /// Password tạm thời do Admin nhập. Nếu null, service generate password ngẫu nhiên
        /// và trả về trong response (chỉ lần này — sau đó không bao giờ lộ).
        public string? TemporaryPassword { get; set; }
    }
}
