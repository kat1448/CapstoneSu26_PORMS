using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Exceptions;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Zone;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Zone
{
    /// Triển khai IZoneService: CRUD zones trong 1 cảng
    /// Role filtering làm ở controller qua HttpContext.IsAuthorizedForPort(portId).
    /// Service tập trung vào nghiệp vụ: validate port, validate zone data, soft delete.
    public sealed class ZoneService : IZoneService
    {
        private readonly IApplicationDbContext _db;

        public ZoneService(IApplicationDbContext db)
        {
            _db = db;
        }

        // READS

        public async Task<IReadOnlyList<ZoneDto>> GetByPortAsync(Guid portId)
        {
            await EnsurePortExistsAsync(portId, requireActive: false);

            var zones = await _db.Zones
                .AsNoTracking()
                .Where(z => z.PortId == portId)
                .OrderBy(z => z.DisplayOrder)
                .ThenBy(z => z.Name)
                .ToListAsync();

            return zones.Select(ToDto).ToList();
        }

        public async Task<ZoneDto?> GetByIdAsync(Guid portId, Guid id)
        {
            var zone = await _db.Zones
                .AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == id && z.PortId == portId);

            return zone is null ? null : ToDto(zone);
        }

        // WRITES

        public async Task<ZoneDto> CreateAsync(
            Guid portId,
            CreateZoneRequest request,
            Guid actorUserId)
        {
            await EnsurePortExistsAsync(portId, requireActive: true);

            var name = ValidateName(request.Name);
            var zoneType = ParseZoneType(request.ZoneType);

            ValidateZoneFields(
                request.Capacity,
                request.Latitude,
                request.Longitude,
                request.DisplayOrder);

            var now = DateTimeOffset.UtcNow;

            var zone = new PORMS.Domain.Entities.Zone
            {
                Id = Guid.NewGuid(),
                PortId = portId,
                Name = name,
                ZoneType = zoneType,
                Description = NormalizeOptionalText(request.Description),
                Capacity = request.Capacity,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsActive = true,
                CurrentRiskLevel = RiskLevel.LOW,
                DisplayOrder = request.DisplayOrder,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Zones.Add(zone);
            await _db.SaveChangesAsync();

            // TODO(audit): ZONE_CREATED when operation event enum supports zone events.
            _ = actorUserId;

            return ToDto(zone);
        }

        public async Task<ZoneDto> UpdateAsync(
            Guid portId,
            Guid id,
            UpdateZoneRequest request,
            Guid actorUserId)
        {
            var zone = await _db.Zones
                .FirstOrDefaultAsync(z => z.Id == id && z.PortId == portId)
                ?? throw new NotFoundException("Không tìm thấy zone trong cảng này");

            var name = ValidateName(request.Name);

            ValidateZoneFields(
                request.Capacity,
                request.Latitude,
                request.Longitude,
                request.DisplayOrder);

            // Không cho đổi PortId hoặc ZoneType để tránh phá vỡ SOP history.
            zone.Name = name;
            zone.Description = NormalizeOptionalText(request.Description);
            zone.Capacity = request.Capacity;
            zone.Latitude = request.Latitude;
            zone.Longitude = request.Longitude;
            zone.DisplayOrder = request.DisplayOrder;

            // COMPANY_ADMIN được phép disable/reactivate zone trong port của họ.
            zone.IsActive = request.IsActive;

            zone.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // TODO(audit): ZONE_UPDATED / ZONE_DISABLED.
            _ = actorUserId;

            return ToDto(zone);
        }

        public async Task SoftDeleteAsync(
            Guid portId,
            Guid id,
            Guid actorUserId)
        {
            var zone = await _db.Zones
                .FirstOrDefaultAsync(z => z.Id == id && z.PortId == portId)
                ?? throw new NotFoundException("Không tìm thấy zone trong cảng này");

            // Idempotent delete: gọi DELETE lần 2 không gây lỗi.
            if (!zone.IsActive)
            {
                return;
            }

            zone.IsActive = false;
            zone.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            // TODO(audit): ZONE_DISABLED.
            _ = actorUserId;
        }

        // HELPERS

        private async Task EnsurePortExistsAsync(Guid portId, bool requireActive)
        {
            var port = await _db.Ports
                .AsNoTracking()
                .Where(p => p.Id == portId)
                .Select(p => new { p.IsActive })
                .FirstOrDefaultAsync();

            if (port is null)
            {
                throw new NotFoundException("Không tìm thấy cảng");
            }

            if (requireActive && !port.IsActive)
            {
                throw new ValidationException("Không thể tạo zone trong cảng đã bị vô hiệu hóa");
            }
        }

        private static string ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ValidationException("Tên zone là bắt buộc");
            }

            var normalized = name.Trim();

            if (normalized.Length > 255)
            {
                throw new ValidationException("Tên zone không được vượt quá 255 ký tự");
            }

            return normalized;
        }

        private static ZoneType ParseZoneType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse<ZoneType>(value, ignoreCase: true, out var zoneType)
                || !Enum.IsDefined(typeof(ZoneType), zoneType))
            {
                throw new ValidationException("ZoneType phải là DOCK, YARD, GATE hoặc WAREHOUSE");
            }

            return zoneType;
        }

        private static void ValidateZoneFields(
            int? capacity,
            decimal? latitude,
            decimal? longitude,
            short displayOrder)
        {
            if (capacity.HasValue && capacity.Value <= 0)
            {
                throw new ValidationException("Capacity phải lớn hơn 0");
            }

            // Tọa độ nên đi theo cặp. Có latitude mà thiếu longitude thì map không dùng được.
            if (latitude.HasValue != longitude.HasValue)
            {
                throw new ValidationException("Latitude và longitude phải được cung cấp cùng nhau");
            }

            if (latitude is < -90 or > 90)
            {
                throw new ValidationException("Latitude phải trong khoảng [-90, 90]");
            }

            if (longitude is < -180 or > 180)
            {
                throw new ValidationException("Longitude phải trong khoảng [-180, 180]");
            }

            if (displayOrder < 0)
            {
                throw new ValidationException("DisplayOrder không được nhỏ hơn 0");
            }
        }

        private static string? NormalizeOptionalText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // MAPPING

        private static ZoneDto ToDto(PORMS.Domain.Entities.Zone z) => new()
        {
            Id = z.Id,
            PortId = z.PortId,
            Name = z.Name,
            ZoneType = z.ZoneType.ToString(),
            Description = z.Description,
            Capacity = z.Capacity,
            Latitude = z.Latitude,
            Longitude = z.Longitude,
            IsActive = z.IsActive,
            CurrentRiskLevel = z.CurrentRiskLevel.ToString(),
            DisplayOrder = z.DisplayOrder,
            CreatedAt = z.CreatedAt,
            UpdatedAt = z.UpdatedAt
        };
    }
}