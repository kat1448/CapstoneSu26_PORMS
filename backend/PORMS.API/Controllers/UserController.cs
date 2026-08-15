using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOrPortManager")]
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
        var access = GetAccessScope(User);
        if (access.Role == "PORT_MANAGER" && access.PortId is null)
        {
            return Forbid();
        }

        var users = await repository.GetUsersAsync(
            search,
            access.Role == "PORT_MANAGER" ? "OPERATOR" : role,
            status,
            portCode,
            cancellationToken,
            access.Role == "PORT_MANAGER" ? access.PortId : null,
            access.Role == "PORT_MANAGER");
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserSummaryResponse>> CreateUser(
        CreateUserRequest request,
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (!CanManagerWrite(access, request.Role, request.PortId))
        {
            return Forbid();
        }

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
        var access = GetAccessScope(User);
        var target = await repository.GetUserAsync(userId, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        if (access.Role == "ADMIN"
            && access.UserId == userId
            && !string.Equals(target.Role, request.Role, StringComparison.Ordinal))
        {
            return Conflict(new
            {
                code = "SELF_ROLE_CHANGE_NOT_ALLOWED",
                message = "Quản trị viên không thể tự thay đổi vai trò của chính mình."
            });
        }

        if (access.Role == "PORT_MANAGER"
            && (access.PortId is null
                || target.Role != "OPERATOR"
                || (target.PortId is not null && target.PortId != access.PortId)
                || !CanManagerWrite(access, request.Role, request.PortId)))
        {
            return Forbid();
        }

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
        var access = GetAccessScope(User);
        if (access.Role == "PORT_MANAGER")
        {
            if (access.PortId is null)
            {
                return Forbid();
            }

            var unassigned = await repository.UnassignOperatorFromPortAsync(userId, access.PortId.Value, cancellationToken);
            return unassigned ? NoContent() : Forbid();
        }

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

        if (role == "PORT_MANAGER" && portId is null)
        {
            return "Port Manager phải được gán cảng phụ trách.";
        }

        return null;
    }

    private static (Guid? UserId, string Role, Guid? PortId) GetAccessScope(ClaimsPrincipal user)
    {
        var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;
        var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var portId = Guid.TryParse(user.FindFirstValue("port_id"), out var parsedPortId)
            ? parsedPortId
            : (Guid?)null;
        return (userId, role, portId);
    }

    private static bool CanManagerWrite((Guid? UserId, string Role, Guid? PortId) access, string targetRole, Guid? targetPortId)
    {
        if (access.Role != "PORT_MANAGER")
        {
            return true;
        }

        return access.PortId is not null
            && targetRole == "OPERATOR"
            && (targetPortId is null || targetPortId == access.PortId);
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
