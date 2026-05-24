using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Zone
{
    /// Body cho POST /api/ports/{portId}/zones (ADMIN, COMPANY_ADMIN của port đó).
    /// PortId lấy từ URL, KHÔNG nhận từ body — chống tạo zone cross-port.
    /// ZoneType phải là DOCK/YARD/GATE/WAREHOUSE — service validate.
    public sealed class CreateZoneRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ZoneType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Capacity { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public short DisplayOrder { get; set; }
    }
}
