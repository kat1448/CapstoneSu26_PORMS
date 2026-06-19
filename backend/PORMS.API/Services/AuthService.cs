using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PORMS.API.Configuration;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly JwtOptions _options;

    public AuthService(UserRepository users, IOptions<JwtOptions> options)
    {
        _users = users;
        _options = options.Value;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await _users.FindForAuthenticationAsync(email, cancellationToken);
        if (user is null || user.Status != "ACTIVE" || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var accessToken = CreateAccessToken(user, now, expiresAt);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        await _users.StoreRefreshTokenAsync(
            user.Id,
            refreshTokenHash,
            now.AddDays(_options.RefreshTokenDays),
            cancellationToken);

        return new LoginResponse(
            accessToken,
            refreshToken,
            expiresAt,
            new AuthUserResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.PortName,
                BuildInitials(user.FullName)));
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ChangePasswordResult.ConfirmationMismatch;
        }

        if (!PasswordPolicy.IsStrong(request.NewPassword))
        {
            return ChangePasswordResult.WeakPassword;
        }

        var user = await _users.FindForAuthenticationAsync(userId, cancellationToken);
        if (user is null)
        {
            return ChangePasswordResult.UserNotFound;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ChangePasswordResult.CurrentPasswordIncorrect;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _users.ChangePasswordAsync(userId, hash, cancellationToken);
        return ChangePasswordResult.Success;
    }

    private string CreateAccessToken(
        AuthUserReadModel user,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            issuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string BuildInitials(string fullName) =>
        string.Concat(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(2)
            .Select(part => char.ToUpperInvariant(part[0])));
}

public enum ChangePasswordResult
{
    Success,
    ConfirmationMismatch,
    WeakPassword,
    CurrentPasswordIncorrect,
    UserNotFound
}
