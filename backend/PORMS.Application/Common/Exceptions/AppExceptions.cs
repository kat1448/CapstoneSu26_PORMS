using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PORMS.Application.Common.Exceptions
{
    /// Lỗi nghiệp vụ chung — middleware map sang HTTP status qua Code/StatusCode.
    public abstract class AppException : System.Exception
    {
        public abstract string Code { get; }
        public abstract int StatusCode { get; }
        protected AppException(string message) : base(message) { }
    }

    /// Dữ liệu không hợp lệ về mặt nghiệp vụ. HTTP 400.
    public sealed class ValidationException : AppException
    {
        public override string Code => "VALIDATION_ERROR";
        public override int StatusCode => 400;
        public ValidationException(string message) : base(message) { }
    }

    /// Resource không tìm thấy. HTTP 404.
    public sealed class NotFoundException : AppException
    {
        public override string Code => "NOT_FOUND";
        public override int StatusCode => 404;
        public NotFoundException(string message) : base(message) { }
    }

    /// Xung đột, vd email đã tồn tại. HTTP 409.
    public sealed class ConflictException : AppException
    {
        private readonly string _code;
        public override string Code => _code;
        public override int StatusCode => 409;
        public ConflictException(string code, string message) : base(message) => _code = code;
    }
}
