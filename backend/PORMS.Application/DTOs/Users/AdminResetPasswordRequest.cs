using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Users
{
    /// Body cho POST /api/users/{id}/reset-password (ADMIN only — US-012).
    /// Reset password user khác (vd: user bị khóa, quên password — internal helpdesk flow).
    /// NewPassword null → service generate ngẫu nhiên và trả về trong response.
    /// Sau khi reset: revoke tất cả refresh tokens, unlock account, ghi PASSWORD_RESET event.
    public sealed class AdminResetPasswordRequest
    {
        public string? NewPassword { get; set; }
    }
}
