using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PORMS.Application.Common;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Infrastructure.Security
{
    /// Triển khai IJwtTokenService bằng System.IdentityModel.Tokens.Jwt.
    /// Ký HMAC-SHA256 với Jwt:SecretKey. Issuer/Audience/TTL đọc từ config —
    /// phải khớp TokenValidationParameters trong Program.cs
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly string _secret;
        private readonly string? _issuer;
        private readonly string? _audience;
        private readonly int _accessTokenMinutes;

        public JwtTokenService(IConfiguration configuration)
        {
            var jwt = configuration.GetSection("Jwt");
            _secret = jwt["SecretKey"]
                      ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                      ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");
            _issuer = jwt["Issuer"];
            _audience = jwt["Audience"];
            _accessTokenMinutes = jwt.GetValue("AccessTokenMinutes", 15);
        }

        public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user)
        {
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddMinutes(_accessTokenMinutes);

            var claims = new List<Claim>
        {
            new(ClaimNames.UserId, user.Id.ToString()),
            new(ClaimNames.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

            // ADMIN không có assigned_port_id; CA/OP thì có.
            if (user.AssignedPortId.HasValue)
            {
                claims.Add(new Claim(ClaimNames.AssignedPortId, user.AssignedPortId.Value.ToString()));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expiresAt.UtcDateTime,
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }

        public string GenerateRefreshToken()
        {
            // 256-bit random, opaque (KHÔNG phải JWT). URL-safe base64.
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        public string HashRefreshToken(string rawRefreshToken)
        {
            // SHA-256: deterministic → tra cứu được bằng WHERE refresh_token_hash
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));
            return Convert.ToHexString(bytes);
        }
    }
}
