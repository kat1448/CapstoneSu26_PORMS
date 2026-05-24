namespace PORMS.API.Middleware
{
    /// Validate JWT Bearer token và inject claims vào HttpContext.User.
    /// Sprint 1: stub — không validate, không inject claims.
    /// Sprint 2: implement với IdentityModel.Tokens — verify signature, expiry,
    /// extract claims (sub=user_id, role, assigned_port_id) và set HttpContext.User.
    /// Note: ASP.NET Core 8 có sẵn AddAuthentication().AddJwtBearer() — có thể
    /// thay middleware này bằng built-in nếu không cần custom logic.
    public sealed class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);
        }
    }
}
