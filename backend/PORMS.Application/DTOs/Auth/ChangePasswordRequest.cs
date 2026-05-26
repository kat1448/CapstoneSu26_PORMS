using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Auth
{
    /// Body cho PUT /api/auth/change-password (authenticated user đổi password của chính mình).
    /// Validation: NewPassword tối thiểu 8 ký tự, có chữ hoa, chữ thường, số.
    /// Service phải verify CurrentPassword trước khi update.
    public sealed class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
