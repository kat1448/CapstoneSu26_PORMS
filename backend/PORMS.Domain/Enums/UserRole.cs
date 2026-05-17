using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Domain.Enums
{
    /// Vai trò người dùng — ánh xạ tới PostgreSQL enum operational.user_role_enum.
    /// Quyết định quyền truy cập (RBAC) trên toàn hệ thống.
    public enum UserRole
    {
        /// Toàn quyền — quản lý users, ports, zones, SOP rules của tất cả cảng.
        ADMIN,

        /// Quản lý 1 cảng — CRUD zones/SOP rules/thresholds của assigned_port_id.
        /// Không tạo/xóa user.
        COMPANY_ADMIN,

        /// Giám sát viên — chỉ đọc dashboard và alerts của assigned_port_id.
        OPERATOR
    }
}
