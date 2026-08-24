using System.Diagnostics;

namespace HealthTracker.Web.Middleware
{

    /// <summary>
    /// Records request outcomes without query strings, headers, cookies, or request bodies.
    /// This gives operators a useful request timeline without copying health data or credentials
    /// into the application log.
    /// </summary>
    public sealed class RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger
    )
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception while processing {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path
                );
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var level = context.Response.StatusCode >= 500
                    ? LogLevel.Error
                    : context.Response.StatusCode >= 400
                        ? LogLevel.Warning
                        : LogLevel.Information;

                logger.Log(
                    level,
                    "HTTP {Method} {Path} completed with status {StatusCode} in {ElapsedMilliseconds} ms.",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds
                );
            }
        }
    }
}
