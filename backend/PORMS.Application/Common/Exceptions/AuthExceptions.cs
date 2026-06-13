using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Common.Exceptions
{
    /// Base cho các lỗi xác thực — middleware map sang HTTP status tương ứng.
    public abstract class AuthExceptions : System.Exception
    {
        public abstract string Code { get; }

        protected AuthExceptions(string message) : base(message) { }
    }

    /// Sai email hoặc password. Dùng CHUNG cho cả 2 trường hợp (email không tồn tại
    /// và password sai) để chống email enumeration. → HTTP 401.
    public sealed class InvalidCredentialsException : AuthExceptions
    {
        public override string Code => "INVALID_CREDENTIALS";
        public InvalidCredentialsException()
            : base("Email hoặc mật khẩu không đúng") { }
    }

    /// Tài khoản bị khóa tạm thời do đăng nhập sai nhiều lần. → HTTP 423.
    public sealed class AccountLockedException : AuthExceptions
    {
        public override string Code => "ACCOUNT_LOCKED";
        public AccountLockedException()
            : base("Tài khoản bị khóa tạm thời do đăng nhập sai nhiều lần. Vui lòng thử lại sau.") { }
    }

    /// Tài khoản INACTIVE hoặc SUSPENDED — không thể đăng nhập. → HTTP 403.
    public sealed class AccountNotActiveException : AuthExceptions
    {
        public override string Code => "ACCOUNT_NOT_ACTIVE";
        public AccountNotActiveException()
            : base("Tài khoản không ở trạng thái hoạt động. Liên hệ quản trị viên.") { }
    }

    /// Refresh token không hợp lệ, hết hạn hoặc đã revoke. → HTTP 401.
    public sealed class InvalidRefreshTokenException : AuthExceptions
    {
        public override string Code => "REFRESH_TOKEN_INVALID";
        public InvalidRefreshTokenException()
            : base("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.") { }
    }

    /// Mật khẩu hiện tại không đúng khi đổi password. → HTTP 422.
    public sealed class InvalidCurrentPasswordException : AuthExceptions
    {
        public override string Code => "INVALID_CURRENT_PASSWORD";
        public InvalidCurrentPasswordException()
            : base("Mật khẩu hiện tại không đúng") { }
    }
}
