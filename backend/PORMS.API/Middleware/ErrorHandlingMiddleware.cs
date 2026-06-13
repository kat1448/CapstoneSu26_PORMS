using PORMS.Application.Common.Exceptions;
using System.Net;
using System.Text.Json;

namespace PORMS.API.Middleware
{
    /// Global exception handler. Catch mọi exception, log, và return JSON { code, message }
    /// (khớp contract auth-zone.yaml). Map AuthException sang HTTP status phù hợp.
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
            catch (AppException ex)
            {
                _logger.LogInformation("App exception {Code} on {Method} {Path}: {Message}",
                    ex.Code, context.Request.Method, context.Request.Path, ex.Message);
                await WriteErrorAsync(context, (HttpStatusCode)ex.StatusCode, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                    "INTERNAL_ERROR", "An unexpected error occurred.");
            }
        }

        private static HttpStatusCode MapAuthStatus(AuthExceptions ex) => ex switch
        {
            InvalidCredentialsException => HttpStatusCode.Unauthorized,          // 401
            InvalidRefreshTokenException => HttpStatusCode.Unauthorized,          // 401
            AccountNotActiveException => HttpStatusCode.Forbidden,             // 403
            AccountLockedException => HttpStatusCode.Locked,                // 423
            InvalidCurrentPasswordException => HttpStatusCode.UnprocessableEntity,  // 422
            _ => HttpStatusCode.BadRequest             // 400 fallback
        };

        private static async Task WriteErrorAsync(
            HttpContext context, HttpStatusCode status, string code, string message)
        {
            var body = new
            {
                code,
                message,
                traceId = context.TraceIdentifier   // extra, FE có thể bỏ qua
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
