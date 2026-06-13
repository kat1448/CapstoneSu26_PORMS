using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.Application.Common;
using PORMS.Application.DTOs.Users;
using PORMS.Application.Services.Users;

namespace PORMS.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public sealed class UserController : ControllerBase
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllAsync(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] string? status,
            [FromQuery] Guid? assignedPortId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

            var (items, total) = await _userService.GetPagedAsync(
                search, role, status, assignedPortId, page, pageSize);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            return Ok(new
            {
                data = items,
                pagination = new { page, pageSize, total, totalPages }
            });
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<CreateUserResult>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request)
        {
            var actorUserId = GetCurrentUserId();
            var result = await _userService.CreateAsync(request, actorUserId);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = result.User.Id }, result);
        }

        [HttpGet("me")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCurrentAsync()
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetCurrentAsync(userId);
            return user is null ? NotFound() : Ok(user);
        }

        [HttpPut("me")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateOwnProfileAsync([FromBody] UpdateOwnProfileRequest request)
        {
            var userId = GetCurrentUserId();
            var user = await _userService.UpdateOwnProfileAsync(userId, request);
            return Ok(user);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            var user = await _userService.GetByIdAsync(id);
            return user is null ? NotFound() : Ok(user);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateUserRequest request)
        {
            var actorUserId = GetCurrentUserId();
            var user = await _userService.UpdateAsync(id, request, actorUserId);
            return Ok(user);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var actorUserId = GetCurrentUserId();
            await _userService.SoftDeleteAsync(id, actorUserId);
            return NoContent();
        }

        [HttpPost("{id:guid}/reset-password")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<AdminResetPasswordResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResetPasswordAsync(
            Guid id, [FromBody] AdminResetPasswordRequest request)
        {
            var actorUserId = GetCurrentUserId();
            var result = await _userService.AdminResetPasswordAsync(id, request, actorUserId);
            return Ok(result);
        }

        [HttpPost("{id:guid}/unlock")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnlockAsync(Guid id)
        {
            var actorUserId = GetCurrentUserId();
            await _userService.UnlockAsync(id, actorUserId);
            return Ok();
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimNames.UserId)?.Value;
            return Guid.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("User id claim missing or invalid.");
        }
    }
}
