using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Exceptions;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.DTOs.Ports;
using PORMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Ports
{
    /// Triển khai IPortService: CRUD cảng. Role filtering (ADMIN vs CA/OP) làm ở controller
    /// qua IsAuthorizedForPort — service ở đây role-agnostic
    public sealed class PortService : IPortService
    {
        private readonly IApplicationDbContext _db;

        public PortService(IApplicationDbContext db)
        {
            _db = db;
        }

        //  READS 

        public async Task<IReadOnlyList<PortDto>> GetAllAsync()
        {
            // Projection: đếm zones active ngay trong query để tránh N+1.
            var ports = await _db.Ports
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new PortDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Timezone = p.Timezone,
                    IsActive = p.IsActive,
                    CurrentMode = p.CurrentMode.ToString(),
                    CurrentRiskLevel = p.CurrentRiskLevel.ToString(),
                    OpenWeatherStationId = p.OpenWeatherStationId,
                    ZoneCount = _db.Zones.Count(z => z.PortId == p.Id && z.IsActive),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return ports;
        }

        public async Task<PortDto?> GetByIdAsync(Guid id)
        {
            var port = await _db.Ports
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new PortDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Timezone = p.Timezone,
                    IsActive = p.IsActive,
                    CurrentMode = p.CurrentMode.ToString(),
                    CurrentRiskLevel = p.CurrentRiskLevel.ToString(),
                    OpenWeatherStationId = p.OpenWeatherStationId,
                    ZoneCount = _db.Zones.Count(z => z.PortId == p.Id && z.IsActive),
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return port;
        }

        //  WRITES 

        public async Task<PortDto> CreateAsync(CreatePortRequest request, Guid actorUserId)
        {
            var code = request.Code.Trim().ToUpperInvariant();

            var codeExists = await _db.Ports.AnyAsync(p => p.Code == code);
            if (codeExists)
            {
                throw new ConflictException("PORT_CODE_EXISTS", "Mã cảng đã tồn tại");
            }

            ValidateCoordinates(request.Latitude, request.Longitude);

            var port = new Port
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Code = code,
                Address = request.Address,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone)
                    ? "Asia/Ho_Chi_Minh"
                    : request.Timezone,
                OpenWeatherStationId = request.OpenWeatherStationId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = actorUserId
                // CurrentMode / CurrentRiskLevel dùng default của entity (NORMAL / LOW).
            };

            _db.Ports.Add(port);
            await _db.SaveChangesAsync();
            // TODO(audit): PORT_CREATED.

            return (await GetByIdAsync(port.Id))!;
        }

        public async Task<PortDto> UpdateAsync(Guid id, UpdatePortRequest request, Guid actorUserId)
        {
            var port = await _db.Ports.FirstOrDefaultAsync(p => p.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy cảng");

            ValidateCoordinates(request.Latitude, request.Longitude);

            port.Name = request.Name.Trim();
            port.Address = request.Address;
            port.Latitude = request.Latitude;
            port.Longitude = request.Longitude;
            if (!string.IsNullOrWhiteSpace(request.Timezone))
            {
                port.Timezone = request.Timezone;
            }
            port.OpenWeatherStationId = request.OpenWeatherStationId;
            port.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            // TODO(audit): PORT_UPDATED.

            return (await GetByIdAsync(port.Id))!;
        }

        public async Task SoftDeleteAsync(Guid id, Guid actorUserId)
        {
            var port = await _db.Ports.FirstOrDefaultAsync(p => p.Id == id)
                       ?? throw new NotFoundException("Không tìm thấy cảng");

            port.IsActive = false;
            port.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            // TODO(audit): PORT_DEACTIVATED.
        }

        //  HELPERS 

        private static void ValidateCoordinates(decimal latitude, decimal longitude)
        {
            if (latitude is < -90 or > 90)
            {
                throw new ValidationException("Latitude phải trong khoảng [-90, 90]");
            }
            if (longitude is < -180 or > 180)
            {
                throw new ValidationException("Longitude phải trong khoảng [-180, 180]");
            }
        }
    }
}
