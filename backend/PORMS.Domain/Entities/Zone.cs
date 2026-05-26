using PORMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Domain.Entities
{
    public class Zone
    {
        /// UUID PK — DB tự sinh qua uuid_generate_v4().
        public Guid Id { get; set; }

        /// FK tới Port chứa zone này. ON DELETE CASCADE ở DB —
        /// xóa Port sẽ xóa tất cả Zone của nó (hard delete hiếm khi xảy ra
        /// vì Port dùng soft delete is_active).
        public Guid PortId { get; set; }

        /// Tên zone — hiển thị trên map và dashboard. VD: "Dock A", "Yard B".
        public string Name { get; set; } = string.Empty;

        /// Loại zone — DOCK / YARD / GATE / WAREHOUSE. SOP engine match theo giá trị này.
        public ZoneType ZoneType { get; set; }

        /// Mô tả thêm về zone (vị trí, đặc điểm, ghi chú).
        public string? Description { get; set; }

        /// Capacity tối đa: TEU cho DOCK, số xe cho YARD, slots cho GATE.
        /// NULL cho phép — không phải zone nào cũng có capacity rõ ràng.
        /// CHECK constraint ở DB: capacity IS NULL OR capacity &gt; 0.
        public int? Capacity { get; set; }

        /// Tọa độ trung tâm zone — hiển thị trên Leaflet map. DECIMAL(9,6) trong DB.
        public decimal? Latitude { get; set; }

        /// Tọa độ trung tâm zone — hiển thị trên Leaflet map. DECIMAL(9,6) trong DB.
        public decimal? Longitude { get; set; }

        /// Trạng thái hoạt động. FALSE = soft-deleted, không hiển thị trên UI.
        public bool IsActive { get; set; } = true;

        /// Risk level hiện tại của zone — có thể khác Port nếu có threshold override.
        /// Cập nhật bởi Risk Engine (BE-C). Default LOW.
        public RiskLevel CurrentRiskLevel { get; set; } = RiskLevel.LOW;

        /// Thứ tự hiển thị trên dashboard map. SMALLINT ở DB.
        public short DisplayOrder { get; set; }

        /// Thời điểm tạo record — DB tự set qua DEFAULT NOW().
        public DateTimeOffset CreatedAt { get; set; }

        /// Thời điểm update cuối.
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
