using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = ["ADMIN", "PORT_MANAGER", "OPERATOR"];
    private static readonly HashSet<string> AllowedStatuses = ["ACTIVE", "INACTIVE", "LOCKED"];

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? portCode,
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var users = await repository.GetUsersAsync(search, role, status, portCode, cancellationToken);
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserSummaryResponse>> CreateUser(
        CreateUserRequest request,
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateWriteRequest(request.Email, request.FullName, request.Role, request.Status, request.PortId);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        if (!PasswordPolicy.IsStrong(request.Password))
        {
            return BadRequest(new { message = "Mật khẩu chưa đáp ứng yêu cầu bảo mật." });
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        var created = await repository.CreateUserAsync(
            new CreateUserReadModel(
                request.Email,
                request.FullName,
                passwordHash,
                request.Role,
                request.Status,
                request.PortId),
            cancellationToken);

        return CreatedAtAction(nameof(GetUsers), new { userId = created.UserId }, ToResponse(created));
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<UserSummaryResponse>> UpdateUser(
        Guid userId,
        UpdateUserRequest request,
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateWriteRequest(request.Email, request.FullName, request.Role, request.Status, request.PortId);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var updated = await repository.UpdateUserAsync(
            userId,
            new UpdateUserReadModel(request.Email, request.FullName, request.Role, request.Status, request.PortId),
            cancellationToken);

        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser(
        Guid userId,
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteUserAsync(userId, cancellationToken);
        return result switch
        {
            DeleteUserResult.Deleted => NoContent(),
            DeleteUserResult.ProtectedAdmin => Conflict(new
            {
                code = "PROTECTED_ADMIN_ACCOUNT",
                message = "Tài khoản quản trị hệ thống được bảo vệ và không thể xoá."
            }),
            _ => NotFound()
        };
    }

    private static UserSummaryResponse ToResponse(UserSummaryReadModel user) =>
        new()
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            PortId = user.PortId,
            Role = user.Role,
            Status = user.Status,
            PortName = user.PortName,
            LastLoginLabel = FormatLastLogin(user.LastLoginAt)
        };

    private static string? ValidateWriteRequest(
        string email,
        string fullName,
        string role,
        string status,
        Guid? portId)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            return "Họ tên và email là bắt buộc.";
        }

        if (!AllowedRoles.Contains(role))
        {
            return "Vai trò không hợp lệ.";
        }

        if (!AllowedStatuses.Contains(status))
        {
            return "Trạng thái không hợp lệ.";
        }

        if (role == "ADMIN" && portId is not null)
        {
            return "ADMIN không gán cảng phụ trách.";
        }

        if (role != "ADMIN" && portId is null)
        {
            return "Người dùng vận hành phải được gán cảng phụ trách.";
        }

        return null;
    }

    private static string FormatLastLogin(DateTimeOffset? lastLoginAt)
    {
        if (lastLoginAt is null)
        {
            return "Chưa đăng nhập";
        }

        var elapsed = DateTimeOffset.UtcNow - lastLoginAt.Value.ToUniversalTime();
        if (elapsed.TotalMinutes < 1)
        {
            return "Vừa xong";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"{Math.Floor(elapsed.TotalHours)} giờ trước";
        }

        return lastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }
}
