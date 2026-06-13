using PORMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Common.Interfaces
{
    /// Sinh JWT access token và refresh token cho user đã xác thực
    /// Claims (user_id, role, assigned_port_id) khớp ClaimNames
    public interface IJwtTokenService
    {
        /// Tạo signed JWT access token
        (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user);

        /// Sinh refresh token thô (opaque random, base64). Controller set vào cookie
        string GenerateRefreshToken();

        /// Hash refresh token (SHA-256) để lưu/đối chiếu trong DB
        string HashRefreshToken(string rawRefreshToken);
    }
}
