using PORMS.Application.DTOs.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Services.Ports
{
    /// Quản lý vòng đời cảng (Port). Mọi write yêu cầu role ADMIN.
    /// Read mở cho mọi role nhưng filter theo AssignedPortId trong service
    public interface IPortService
    {
        /// ADMIN tạo cảng mới (US-015). Validate code unique, lat/long trong range.
        /// Code immutable sau khi tạo.
        Task<PortDto> CreateAsync(CreatePortRequest request, Guid actorUserId);

        /// Lấy chi tiết cảng. Null nếu không tồn tại hoặc đã soft-delete (IsActive=false)
        Task<PortDto?> GetByIdAsync(Guid id);

        /// Danh sách tất cả cảng (US-016).
        /// ADMIN thấy tất cả; COMPANY_ADMIN/OPERATOR chỉ thấy AssignedPortId của họ.
        /// Filter theo role thực hiện ở service hoặc controller dùng IsAuthorizedForPort.
        Task<IReadOnlyList<PortDto>> GetAllAsync();

        /// ADMIN sửa thông tin cảng. Code KHÔNG sửa được (không có field trong DTO)
        Task<PortDto> UpdateAsync(Guid id, UpdatePortRequest request, Guid actorUserId);

        /// Soft delete: set IsActive=false. Không xóa data lịch sử.
        /// Khi cảng inactive: dừng fetch weather, không cho login user assigned port này
        Task SoftDeleteAsync(Guid id, Guid actorUserId);
    }
}
