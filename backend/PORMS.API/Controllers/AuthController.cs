using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.Application.Common;
using PORMS.Application.DTOs.Auths;
using PORMS.Application.Services.Auths;

namespace PORMS.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private const string RefreshCookieName = "porms_refresh";

        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;

        public AuthController(IAuthService authService, IWebHostEnvironment env)
        {
            _authService = authService;
            _env = env;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status423Locked)]
        public async Task<ActionResult<LoginResponse>> LoginAsync(
            [FromBody] LoginRequest request)
        {
            var deviceInfo = Request.Headers.UserAgent.ToString();
            var result = await _authService.LoginAsync(request, deviceInfo);

            SetRefreshCookie(result.RawRefreshToken, result.RefreshTokenExpiresAt);
            return Ok(result.Response);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> RefreshAsync()
        {
            var rawRefreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
            var result = await _authService.RefreshTokenAsync(rawRefreshToken);

            SetRefreshCookie(result.RawRefreshToken, result.RefreshTokenExpiresAt);
            return Ok(result.Response);
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LogoutAsync()
        {
            var userId = GetCurrentUserId();
            await _authService.LogoutAsync(userId);

            Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTimeOffset.UtcNow));
            return NoContent();
        }

        [HttpPut("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> ChangePasswordAsync(
            [FromBody] ChangePasswordRequest request)
        {
            var userId = GetCurrentUserId();
            await _authService.ChangePasswordAsync(userId, request);

            // Đổi password đã revoke refresh token → clear cookie, buộc đăng nhập lại.
            Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTimeOffset.UtcNow));
            return Ok();
        }

        // ---- helpers ----

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimNames.UserId)?.Value;
            return Guid.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("User id claim missing or invalid.");
        }

        private void SetRefreshCookie(string rawToken, DateTimeOffset expiresAt)
            => Response.Cookies.Append(RefreshCookieName, rawToken, BuildCookieOptions(expiresAt));

        private CookieOptions BuildCookieOptions(DateTimeOffset expiresAt) => new()
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),   // dev qua http vẫn set được; prod bắt buộc https
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/api/auth"
        };
    }
}
