using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Domain.Enums
{
    public enum ZoneType
    {
        /// <summary>Cầu cảng — nơi tàu cập. Nhạy cảm nhất với gió và sóng.</summary>
        DOCK,

        /// <summary>Bãi container — xếp dỡ và lưu kho ngoài trời.</summary>
        YARD,

        /// <summary>Cổng cảng — kiểm soát ra/vào xe và hàng hóa.</summary>
        GATE,

        /// <summary>Kho hàng có mái che — ít chịu ảnh hưởng thời tiết hơn các zone khác.</summary>
        WAREHOUSE
    }
}
