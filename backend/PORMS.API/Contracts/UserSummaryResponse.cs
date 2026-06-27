namespace PORMS.API.Contracts;

public sealed class UserSummaryResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid? PortId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PortName { get; set; } = "Tất cả";
    public string LastLoginLabel { get; set; } = "Chưa đăng nhập";
}

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    string Role,
    string Status,
    Guid? PortId);

public sealed record UpdateUserRequest(
    string Email,
    string FullName,
    string Role,
    string Status,
    Guid? PortId);
