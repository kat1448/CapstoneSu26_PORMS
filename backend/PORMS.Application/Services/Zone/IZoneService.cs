using PORMS.Application.DTOs.Zone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Zone
{
    /// Quản lý zones trong cảng. Mọi method nhận portId từ URL —
    /// service validate zone thuộc đúng port (defense in depth).
    public interface IZoneService
    {
        /// ADMIN hoặc COMPANY_ADMIN của port tạo zone (US-020). Validate ZoneType hợp lệ,
        /// Capacity > 0 nếu có giá trị.
        Task<ZoneDto> CreateAsync(Guid portId, CreateZoneRequest request, Guid actorUserId);

        /// Lấy chi tiết 1 zone. Validate zone.PortId == portId — trả null nếu không match
        /// (chống path traversal: GET /ports/{portA}/zones/{zoneOfPortB})
        Task<ZoneDto?> GetByIdAsync(Guid portId, Guid id);

        /// Danh sách zones của 1 port (active + inactive). Sort theo DisplayOrder
        Task<IReadOnlyList<ZoneDto>> GetByPortAsync(Guid portId);

        /// Sửa zone. KHÔNG sửa được PortId hoặc ZoneType (không có field trong DTO).
        /// Có thể toggle IsActive để soft-undelete
        Task<ZoneDto> UpdateAsync(Guid portId, Guid id, UpdateZoneRequest request, Guid actorUserId);

        /// Soft delete: set IsActive=false
        Task SoftDeleteAsync(Guid portId, Guid id, Guid actorUserId);
    }
}
