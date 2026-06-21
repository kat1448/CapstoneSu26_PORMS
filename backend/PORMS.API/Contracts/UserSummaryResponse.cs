namespace PORMS.API.Contracts;

public sealed class UserSummaryResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PortName { get; set; } = "Tất cả";
    public string LastLoginLabel { get; set; } = "Chưa đăng nhập";
}
