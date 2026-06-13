using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Common.Interfaces
{
    /// Trừu tượng hóa việc hash/verify password
    public interface IPasswordHasher
    {
        /// Hash password plaintext. Trả về chuỗi hash kèm salt và work factor
        string Hash(string password);

        /// So sánh password plaintext với hash đã lưu. True nếu khớp
        bool Verify(string password, string hash);
    }
}
