using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Ports
{
    /// Body cho POST /api/ports (ADMIN only — US-015).
    /// Code là immutable sau khi tạo (US-015: "Code không thay đổi sau khi tạo").
    /// Latitude ∈ [-90, 90], Longitude ∈ [-180, 180] — service validate.
    public sealed class CreatePortRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public string? OpenWeatherStationId { get; set; }
    }
}
