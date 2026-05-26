using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Ports
{
    /// Body cho PUT /api/ports/{id} (ADMIN only).
    /// KHÔNG cho phép sửa Code (immutable). Tách riêng khỏi CreatePortRequest để
    /// type system bảo vệ — không thể accident gửi Code qua đường update.
    /// IsActive sửa qua DELETE /api/ports/{id} (soft delete) hoặc một endpoint reactivate riêng.
    public sealed class UpdatePortRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public string? OpenWeatherStationId { get; set; }
    }
}
