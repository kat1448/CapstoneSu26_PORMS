using System.Net;
using System.Text.Json;

namespace PORMS.API.Middleware
{
    /// Global exception handler. Catch mọi exception không xử lý từ controller/service,
    /// log lỗi, và return ProblemDetails (RFC 7807) thay vì stack trace.
    /// Phải là middleware OUTERMOST trong pipeline.
    public sealed class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                await WriteProblemDetailsAsync(context, HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.");
            }
        }

        private static async Task WriteProblemDetailsAsync(
            HttpContext context, HttpStatusCode status, string detail)
        {
            var problem = new
            {
                type = $"https://httpstatuses.com/{(int)status}",
                title = status.ToString(),
                status = (int)status,
                detail,
                instance = context.Request.Path.Value,
                traceId = context.TraceIdentifier
            };

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
