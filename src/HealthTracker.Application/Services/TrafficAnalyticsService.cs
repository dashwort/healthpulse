using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    /// <summary>Aggregates privacy-minimized request histograms for the administrator traffic view.</summary>
    public sealed class TrafficAnalyticsService(IHealthDataStore dataStore, ICurrentUser currentUser)
    {
        public async Task<TrafficSummaryDto> GetSummaryAsync(string range, CancellationToken ct)
        {
            await RequireAdministratorAsync(ct);
            var window = CreateWindow(range, DateTimeOffset.UtcNow);
            var previousFromUtc = window.FromUtc - window.Duration;
            var currentWindowStartTicks = FloorToMetricBucket(window.FromUtc.UtcTicks);
            var previousWindowStartTicks = FloorToMetricBucket(previousFromUtc.UtcTicks);
            var buckets = (await dataStore.GetTrafficMetricBucketsAsync(
                    previousWindowStartTicks,
                    window.ToUtc.UtcTicks,
                    ct
                ))
                // Ignore older self-generated diagnostics requests as well as new requests skipped at capture time.
                .Where(bucket => !IsDiagnosticsRoute(bucket.RouteTemplate))
                .ToArray();
            var current = buckets.Where(bucket => bucket.BucketStartUtcTicks >= currentWindowStartTicks).ToArray();
            var previous = buckets.Where(bucket => bucket.BucketStartUtcTicks < currentWindowStartTicks).ToArray();

            var totalRequests = current.Sum(bucket => bucket.RequestCount);
            var previousRequests = previous.Sum(bucket => bucket.RequestCount);
            var (successful, clientErrors, rateLimited, serverErrors) = CountResponses(current);
            var timeline = CreateTimeline(current, window);
            var topRoutes = current
                .GroupBy(bucket => (bucket.RouteTemplate, bucket.Method))
                .Select(group => new TrafficRouteSummaryDto(
                    group.Key.RouteTemplate,
                    group.Key.Method,
                    group.Sum(bucket => bucket.RequestCount),
                    group.Where(bucket => bucket.StatusCode >= 400).Sum(bucket => bucket.RequestCount),
                    GetQuantileBand(group, 0.95)
                ))
                .OrderByDescending(route => route.RequestCount)
                .ThenBy(route => route.RouteTemplate, StringComparer.Ordinal)
                .Take(10)
                .ToArray();
            var topStatuses = current
                .GroupBy(bucket => bucket.StatusCode)
                .Select(group => new TrafficStatusSummaryDto(group.Key, group.Sum(bucket => bucket.RequestCount)))
                .OrderByDescending(status => status.RequestCount)
                .ThenBy(status => status.StatusCode)
                .Take(6)
                .ToArray();

            double? change = previousRequests == 0
                ? null
                : Math.Round(((double)totalRequests - previousRequests) / previousRequests * 100, 1);

            return new TrafficSummaryDto(
                window.Range,
                window.FromUtc,
                window.ToUtc,
                totalRequests,
                previousRequests,
                change,
                successful,
                clientErrors,
                rateLimited,
                serverErrors,
                GetQuantileBand(current, 0.50),
                GetQuantileBand(current, 0.95),
                timeline,
                topRoutes,
                topStatuses
            );
        }

        private async Task RequireAdministratorAsync(CancellationToken ct)
        {
            var normalizedEmail = currentUser.Email.Trim().ToUpperInvariant();
            var allowedUser = string.IsNullOrEmpty(normalizedEmail)
                ? null
                : await dataStore.FindAllowedUserByEmailAsync(normalizedEmail, false, ct);
            if (allowedUser?.Role != AllowedUserRole.Admin)
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static (long Successful, long ClientErrors, long RateLimited, long ServerErrors) CountResponses(
            IEnumerable<TrafficMetricBucket> buckets
        )
        {
            long successful = 0;
            long clientErrors = 0;
            long rateLimited = 0;
            long serverErrors = 0;
            foreach (var bucket in buckets)
            {
                if (bucket.StatusCode is >= 200 and < 400)
                {
                    successful += bucket.RequestCount;
                }
                else if (bucket.StatusCode == 429)
                {
                    rateLimited += bucket.RequestCount;
                }
                else if (bucket.StatusCode is >= 400 and < 500)
                {
                    clientErrors += bucket.RequestCount;
                }
                else if (bucket.StatusCode >= 500)
                {
                    serverErrors += bucket.RequestCount;
                }
            }

            return (successful, clientErrors, rateLimited, serverErrors);
        }

        private static IReadOnlyCollection<TrafficTimelineBucketDto> CreateTimeline(
            IReadOnlyCollection<TrafficMetricBucket> buckets,
            TrafficWindow window
        )
        {
            var result = new TrafficTimelineBucketDto[window.BucketCount];
            var bucketGroups = Enumerable.Range(0, window.BucketCount)
                .Select(_ => new List<TrafficMetricBucket>())
                .ToArray();
            foreach (var bucket in buckets)
            {
                var offsetTicks = bucket.BucketStartUtcTicks - window.FromUtc.UtcTicks;
                if (offsetTicks < -TimeSpan.FromMinutes(5).Ticks)
                {
                    continue;
                }

                var index = Math.Max(0, (int)(offsetTicks / window.BucketSpan.Ticks));
                if (index < bucketGroups.Length)
                {
                    bucketGroups[index].Add(bucket);
                }
            }

            for (var index = 0; index < result.Length; index++)
            {
                var start = window.FromUtc.AddTicks(window.BucketSpan.Ticks * index);
                var (successful, clientErrors, rateLimited, serverErrors) = CountResponses(bucketGroups[index]);
                result[index] = new TrafficTimelineBucketDto(
                    start,
                    successful,
                    clientErrors,
                    rateLimited,
                    serverErrors
                );
            }

            return result;
        }

        private static long FloorToMetricBucket(long utcTicks)
        {
            var bucketTicks = TimeSpan.FromMinutes(5).Ticks;
            return utcTicks - utcTicks % bucketTicks;
        }

        private static bool IsDiagnosticsRoute(string routeTemplate)
        {
            var normalized = routeTemplate.TrimStart('/');
            return normalized.Equals("api/admin/diagnostics", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("api/admin/diagnostics/", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("admin/logs/raw", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("admin/logs/download", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetQuantileBand(IEnumerable<TrafficMetricBucket> buckets, double quantile)
        {
            var counts = buckets
                .GroupBy(bucket => bucket.LatencyBand)
                .ToDictionary(group => group.Key, group => group.Sum(bucket => bucket.RequestCount));
            var total = counts.Values.Sum();
            if (total == 0)
            {
                return -1;
            }

            var target = Math.Max(1, (long)Math.Ceiling(total * quantile));
            long seen = 0;
            foreach (var (band, count) in counts.OrderBy(entry => entry.Key))
            {
                seen += count;
                if (seen >= target)
                {
                    return band;
                }
            }

            return -1;
        }

        private static TrafficWindow CreateWindow(string range, DateTimeOffset nowUtc)
        {
            var (normalizedRange, duration, bucketCount) = range.Trim().ToLowerInvariant() switch
            {
                "24h" => ("24h", TimeSpan.FromHours(24), 24),
                "7d" => ("7d", TimeSpan.FromDays(7), 7),
                "30d" => ("30d", TimeSpan.FromDays(30), 30),
                _ => throw new ArgumentException("The traffic range must be 24h, 7d, or 30d.", nameof(range)),
            };
            var toUtc = nowUtc.ToUniversalTime();
            return new TrafficWindow(
                normalizedRange,
                toUtc - duration,
                toUtc,
                duration,
                bucketCount,
                TimeSpan.FromTicks(duration.Ticks / bucketCount)
            );
        }

        private sealed record TrafficWindow(
            string Range,
            DateTimeOffset FromUtc,
            DateTimeOffset ToUtc,
            TimeSpan Duration,
            int BucketCount,
            TimeSpan BucketSpan
        );
    }

}
