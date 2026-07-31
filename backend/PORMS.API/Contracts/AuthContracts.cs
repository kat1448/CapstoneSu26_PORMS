namespace PORMS.API.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string Name,
    string Role,
    Guid? PortId,
    string PortName,
    string Initials);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    AuthUserResponse User);
