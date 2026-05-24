using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.DTOs.Zone
{
    /// Đại diện zone trong tất cả response của API.
    public sealed class ZoneDto
    {
        public Guid Id { get; set; }
        public Guid PortId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ZoneType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? Capacity { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public bool IsActive { get; set; }
        public string CurrentRiskLevel { get; set; } = string.Empty;
        public short DisplayOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
