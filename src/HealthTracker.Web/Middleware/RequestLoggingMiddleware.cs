using System.Diagnostics;

using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Diagnostics;

namespace HealthTracker.Web.Middleware
{

    /// <summary>
    /// Aggregates request outcomes without query strings, headers, cookies, or request bodies.
    /// Routine successful requests stay out of the production text log; errors and slow requests
    /// remain available for investigation.
    /// </summary>
    public sealed class RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger
    )
    {
        public async Task InvokeAsync(HttpContext context, TrafficMetricBuffer trafficMetrics)
        {
            var isPipelineReexecution =
                context.Features.Get<IExceptionHandlerPathFeature>() is not null
                || context.Features.Get<IStatusCodeReExecuteFeature>() is not null;
            var stopwatch = Stopwatch.StartNew();
            var unhandledException = false;
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                unhandledException = true;
                logger.LogError(
                    exception,
                    "Unhandled exception while processing {Method} {RouteTemplate}.",
                    context.Request.Method,
                    TrafficMetricBuffer.GetRouteTemplate(context)
                );
                throw;
            }
            finally
            {
                stopwatch.Stop();
                if (!isPipelineReexecution)
                {
                    var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    var routeTemplate = TrafficMetricBuffer.GetRouteTemplate(context);
                    var statusCode = unhandledException
                        ? StatusCodes.Status500InternalServerError
                        : context.Response.StatusCode;
                    trafficMetrics.Record(context, statusCode, elapsedMilliseconds);
                    var level = statusCode >= 500
                        ? LogLevel.Error
                        : statusCode >= 400
                            ? LogLevel.Warning
                            : elapsedMilliseconds >= 750
                                ? LogLevel.Warning
                                : LogLevel.Debug;

                    logger.Log(
                        level,
                        "HTTP {Method} {RouteTemplate} completed with status {StatusCode} in {ElapsedMilliseconds} ms.",
                        context.Request.Method,
                        routeTemplate,
                        statusCode,
                        elapsedMilliseconds
                    );
                }
            }
        }
    }
}
