using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Ports
{
    /// Đại diện cảng trong tất cả response của API.
    /// CurrentMode và CurrentRiskLevel cập nhật bởi BE-C/BE-D — read-only ở module BE-B.
    public sealed class PortDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Timezone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CurrentMode { get; set; } = string.Empty;
        public string CurrentRiskLevel { get; set; } = string.Empty;
        public string? OpenWeatherStationId { get; set; }
        public int ZoneCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
