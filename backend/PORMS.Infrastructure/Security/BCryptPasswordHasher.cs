using PORMS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Infrastructure.Security
{
    /// Triển khai IPasswordHasher bằng BCrypt.Net-Next với work factor 12
    /// Work factor 12 ≈ 250ms/hash — đủ chậm để chống brute-force, đủ nhanh để login mượt
    /// Verify tự đọc work factor từ chính hash, nên vẫn verify được hash tạo với factor khác
    public sealed class BCryptPasswordHasher : IPasswordHasher
    {
        private const int WorkFactor = 12;

        public string Hash(string password)
            => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

        public bool Verify(string password, string hash)
            => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
