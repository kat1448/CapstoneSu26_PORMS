using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Auths
{
    /// Body cho POST /api/auth/login.
    /// Validation: Email phải đúng format RFC 5322, Password không rỗng.
    public sealed class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
