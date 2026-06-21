using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;

namespace PORMS.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        [FromServices] AuthService authService,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return response is null
            ? Unauthorized(new { code = "INVALID_CREDENTIALS", message = "Email hoặc mật khẩu không đúng." })
            : Ok(response);
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        [FromServices] AuthService authService,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(userId, request, cancellationToken);
        return result switch
        {
            ChangePasswordResult.Success => Ok(new { message = "Mật khẩu đã được đổi." }),
            ChangePasswordResult.ConfirmationMismatch => BadRequest(new { code = "PASSWORD_MISMATCH", message = "Mật khẩu xác nhận không khớp." }),
            ChangePasswordResult.WeakPassword => BadRequest(new { code = "WEAK_PASSWORD", message = "Mật khẩu chưa đáp ứng yêu cầu bảo mật." }),
            ChangePasswordResult.CurrentPasswordIncorrect => UnprocessableEntity(new { code = "CURRENT_PASSWORD_INCORRECT", message = "Mật khẩu hiện tại không đúng." }),
            _ => Unauthorized()
        };
    }
}
