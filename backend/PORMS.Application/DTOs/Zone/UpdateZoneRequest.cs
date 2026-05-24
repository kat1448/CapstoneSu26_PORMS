using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Zone
{
    /// Body cho PUT /api/ports/{portId}/zones/{id}.
    /// KHÔNG cho phép đổi PortId (zone không di chuyển giữa ports — xóa và tạo lại).
    /// KHÔNG cho phép đổi ZoneType (đổi kiểu zone phá vỡ SOP history — xóa và tạo mới).
    /// CurrentRiskLevel cập nhật bởi Risk Engine, không sửa qua endpoint này.
    public sealed class UpdateZoneRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Capacity { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public short DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
