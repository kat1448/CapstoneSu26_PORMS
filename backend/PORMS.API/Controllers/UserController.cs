using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryResponse>>> GetUsers(
        [FromServices] UserRepository repository,
        CancellationToken cancellationToken)
    {
        var users = await repository.GetUsersAsync(cancellationToken);

        return Ok(users.Select(user => new UserSummaryResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            Status = user.Status,
            PortName = user.PortName,
            LastLoginLabel = FormatLastLogin(user.LastLoginAt)
        }).ToList());
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
