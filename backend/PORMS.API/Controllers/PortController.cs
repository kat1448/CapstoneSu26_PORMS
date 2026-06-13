using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Extensions;
using PORMS.Application.Common;
using PORMS.Application.DTOs.Ports;
using PORMS.Application.Services.Ports;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers
{
    [ApiController]
    [Route("api/ports")]
    [Authorize]
    public sealed class PortController : ControllerBase
    {
        private readonly IPortService _portService;

        public PortController(IPortService portService)
        {
            _portService = portService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllAsync()
        {
            var all = await _portService.GetAllAsync();

            if (IsAdmin())
            {
                return Ok(all);
            }

            var assignedPortId = GetAssignedPortId();
            var filtered = all.Where(p => p.Id == assignedPortId && p.IsActive).ToList();
            return Ok(filtered);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<PortDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(Guid id)
        {
            // Kiểm tra quyền truy cập port này trước khi trả data.
            if (!HttpContext.IsAuthorizedForPort(id))
            {
                return Forbid();
            }

            var port = await _portService.GetByIdAsync(id);
            return port is null ? NotFound() : Ok(port);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<PortDto>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePortRequest request)
        {
            var actorUserId = GetCurrentUserId();
            var port = await _portService.CreateAsync(request, actorUserId);
            return StatusCode(StatusCodes.Status201Created, port);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType<PortDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdatePortRequest request)
        {
            var actorUserId = GetCurrentUserId();
            var port = await _portService.UpdateAsync(id, request, actorUserId);
            return Ok(port);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOnly")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid id)
        {
            var actorUserId = GetCurrentUserId();
            await _portService.SoftDeleteAsync(id, actorUserId);
            return NoContent();
        }

        //  helpers 

        private bool IsAdmin()
            => string.Equals(
                User.FindFirst(ClaimNames.Role)?.Value,
                nameof(UserRole.ADMIN),
                StringComparison.Ordinal);

        private Guid? GetAssignedPortId()
            => Guid.TryParse(User.FindFirst(ClaimNames.AssignedPortId)?.Value, out var id)
                ? id
                : null;

        private Guid GetCurrentUserId()
            => Guid.TryParse(User.FindFirst(ClaimNames.UserId)?.Value, out var id)
                ? id
                : throw new UnauthorizedAccessException("User id claim missing or invalid.");
    }
}
