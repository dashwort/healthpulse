using System.Collections.Concurrent;

using HealthTracker.Domain.Models;

using Microsoft.AspNetCore.Routing;

namespace HealthTracker.Web.Services
{

    /// <summary>Aggregates request counts in memory before they are flushed as low-cardinality histograms.</summary>
    public sealed class TrafficMetricBuffer
    {
        private static readonly long BucketDurationTicks = TimeSpan.FromMinutes(5).Ticks;
        private readonly ConcurrentDictionary<MetricKey, TrafficMetricBucket> pending = new();

        public void Record(HttpContext context, int responseStatusCode, long elapsedMilliseconds)
        {
            if (IsDiagnosticsRequest(context))
            {
                return;
            }

            var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
            var bucketStartTicks = nowTicks - nowTicks % BucketDurationTicks;
            var routeTemplate = GetRouteTemplate(context);
            var method = NormalizeMethod(context.Request.Method);
            var statusCode = Math.Clamp(responseStatusCode, 100, 599);
            var latencyBand = GetLatencyBand(elapsedMilliseconds);
            var key = new MetricKey(bucketStartTicks, routeTemplate, method, statusCode, latencyBand);
            var elapsed = Math.Max(0, elapsedMilliseconds);
            pending.AddOrUpdate(
                key,
                _ => new TrafficMetricBucket(
                    bucketStartTicks,
                    routeTemplate,
                    method,
                    statusCode,
                    latencyBand,
                    1,
                    elapsed,
                    elapsed
                ),
                (_, current) => current with
                {
                    RequestCount = current.RequestCount + 1,
                    TotalElapsedMilliseconds = current.TotalElapsedMilliseconds + elapsed,
                    MaxElapsedMilliseconds = Math.Max(current.MaxElapsedMilliseconds, elapsed),
                }
            );
        }

        public IReadOnlyCollection<TrafficMetricBucket> Drain()
        {
            var drained = new List<TrafficMetricBucket>();
            foreach (var key in pending.Keys)
            {
                if (pending.TryRemove(key, out var metric))
                {
                    drained.Add(metric);
                }
            }

            return drained;
        }

        public void Restore(IEnumerable<TrafficMetricBucket> metrics)
        {
            foreach (var metric in metrics)
            {
                var key = new MetricKey(
                    metric.BucketStartUtcTicks,
                    metric.RouteTemplate,
                    metric.Method,
                    metric.StatusCode,
                    metric.LatencyBand
                );
                pending.AddOrUpdate(
                    key,
                    metric,
                    (_, current) => current with
                    {
                        RequestCount = current.RequestCount + metric.RequestCount,
                        TotalElapsedMilliseconds = current.TotalElapsedMilliseconds + metric.TotalElapsedMilliseconds,
                        MaxElapsedMilliseconds = Math.Max(current.MaxElapsedMilliseconds, metric.MaxElapsedMilliseconds),
                    }
                );
            }
        }

        public static string GetRouteTemplate(HttpContext context)
        {
            if (context.GetEndpoint() is RouteEndpoint endpoint
                && !string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
            {
                var routeTemplate = endpoint.RoutePattern.RawText.Trim();
                return routeTemplate.Length <= 180 ? routeTemplate : routeTemplate[..180];
            }

            // Do not persist arbitrary unmatched paths; they can contain identifiers or user-supplied data.
            return "unmatched";
        }

        private static bool IsDiagnosticsRequest(HttpContext context)
        {
            if (context.GetEndpoint() is not RouteEndpoint endpoint
                || string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
            {
                return false;
            }

            var routeTemplate = endpoint.RoutePattern.RawText.TrimStart('/');
            return routeTemplate.Equals("api/admin/diagnostics", StringComparison.OrdinalIgnoreCase)
                || routeTemplate.StartsWith("api/admin/diagnostics/", StringComparison.OrdinalIgnoreCase)
                || routeTemplate.Equals("admin/logs/raw", StringComparison.OrdinalIgnoreCase)
                || routeTemplate.Equals("admin/logs/download", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMethod(string method) => method.ToUpperInvariant() switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            _ => "OTHER",
        };

        private static int GetLatencyBand(long elapsedMilliseconds) => elapsedMilliseconds switch
        {
            < 50 => 0,
            < 100 => 1,
            < 250 => 2,
            < 500 => 3,
            < 1000 => 4,
            < 2000 => 5,
            < 5000 => 6,
            _ => 7,
        };

        private readonly record struct MetricKey(
            long BucketStartUtcTicks,
            string RouteTemplate,
            string Method,
            int StatusCode,
            int LatencyBand
        );
    }
}
