namespace PORMS.API.Middleware
{
    /// Kiểm tra role và data isolation theo AssignedPortId (US-007).
    /// Phải chạy SAU JwtMiddleware để có sẵn claims trong HttpContext.User.
    /// Sprint 1: stub — pass through tất cả.
    /// Sprint 2: implement role check + port-level data isolation.
    public sealed class RbacMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RbacMiddleware> _logger;

        public RbacMiddleware(RequestDelegate next, ILogger<RbacMiddleware> logger)
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
