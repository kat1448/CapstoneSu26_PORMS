using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Extensions;
using PORMS.Application.Common;
using PORMS.Application.DTOs.Zone;
using PORMS.Application.Services.Zone;
using PORMS.Domain.Enums;

namespace PORMS.API.Controllers
{
    [ApiController]
    [Route("api/ports/{portId:guid}/zones")]
    [Authorize]
    public sealed class ZoneController : ControllerBase
    {
        private readonly IZoneService _zoneService;

        private const string GetZoneByIdRouteName = "GetZoneById";

        public ZoneController(IZoneService zoneService)
        {
            _zoneService = zoneService;
        }

        // READS

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<ZoneDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByPortAsync(Guid portId)
        {
            if (!HttpContext.IsAuthorizedForPort(portId))
            {
                return Forbid();
            }

            var zones = await _zoneService.GetByPortAsync(portId);

            // Operator chỉ thấy zones đang hoạt động.
            // Admin/CompanyAdmin cần thấy cả inactive zones để quản lý/reactivate.
            if (!CanManageZones())
            {
                zones = zones.Where(z => z.IsActive).ToList();
            }

            return Ok(zones);
        }

        [HttpGet("{id:guid}", Name = GetZoneByIdRouteName)]
        [ProducesResponseType<ZoneDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(Guid portId, Guid id)
        {
            if (!HttpContext.IsAuthorizedForPort(portId))
            {
                return Forbid();
            }

            var zone = await _zoneService.GetByIdAsync(portId, id);

            if (zone is null)
            {
                return NotFound();
            }

            // Không expose inactive zone cho Operator.
            if (!zone.IsActive && !CanManageZones())
            {
                return NotFound();
            }

            return Ok(zone);
        }

        // WRITES

        [HttpPost]
        [Authorize(Policy = "AdminOrCompanyAdmin")]
        [ProducesResponseType<ZoneDto>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateAsync(
            Guid portId,
            [FromBody] CreateZoneRequest request)
        {
            if (!HttpContext.IsAuthorizedForPort(portId))
            {
                return Forbid();
            }

            var actorUserId = GetCurrentUserId();

            var zone = await _zoneService.CreateAsync(portId, request, actorUserId);

            return CreatedAtRoute(
                GetZoneByIdRouteName,
                new { portId, id = zone.Id },
                zone);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AdminOrCompanyAdmin")]
        [ProducesResponseType<ZoneDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAsync(
            Guid portId,
            Guid id,
            [FromBody] UpdateZoneRequest request)
        {
            if (!HttpContext.IsAuthorizedForPort(portId))
            {
                return Forbid();
            }

            var actorUserId = GetCurrentUserId();

            var zone = await _zoneService.UpdateAsync(portId, id, request, actorUserId);

            return Ok(zone);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AdminOrCompanyAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid portId, Guid id)
        {
            if (!HttpContext.IsAuthorizedForPort(portId))
            {
                return Forbid();
            }

            var actorUserId = GetCurrentUserId();

            await _zoneService.SoftDeleteAsync(portId, id, actorUserId);

            return NoContent();
        }

        // HELPERS

        private bool CanManageZones()
        {
            var role = User.FindFirst(ClaimNames.Role)?.Value;

            return role is nameof(UserRole.ADMIN)
                or nameof(UserRole.COMPANY_ADMIN);
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