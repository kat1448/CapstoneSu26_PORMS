using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Common
{
    /// Tên các custom claims trong JWT
    /// JwtTokenService (Infrastructure) khi sinh token và HttpContextExtensions
    public static class ClaimNames
    {
        /// User ID (Guid). Dùng "user_id" cho rõ ràng thay vì chỉ "sub"
        public const string UserId = "user_id";

        /// Role: ADMIN / COMPANY_ADMIN / OPERATOR
        public const string Role = "role";

        /// Port được phân công (Guid). Vắng mặt = ADMIN (toàn quyền)
        public const string AssignedPortId = "assigned_port_id";
    }
}
