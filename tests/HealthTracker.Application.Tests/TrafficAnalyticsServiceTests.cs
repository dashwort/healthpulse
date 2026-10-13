using AwesomeAssertions;

using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;

namespace HealthTracker.Application.Tests
{

    public sealed class TrafficAnalyticsServiceTests
    {
        [Fact]
        public async Task Summary_aggregates_responses_latency_and_previous_period()
        {
            var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
            var currentBucketStart = nowTicks - nowTicks % TimeSpan.FromMinutes(5).Ticks;
            var store = new TestDataStore();
            store.TrafficMetricBuckets.AddRange(
                [
                    new TrafficMetricBucket(currentBucketStart, "/api/readings", "GET", 200, 0, 2, 24, 16),
                    new TrafficMetricBucket(currentBucketStart, "/api/readings", "GET", 429, 4, 1, 800, 800),
                    new TrafficMetricBucket(currentBucketStart, "/api/readings/{id}", "GET", 500, 7, 1, 6000, 6000),
                    new TrafficMetricBucket(
                        currentBucketStart - TimeSpan.FromHours(25).Ticks,
                        "/api/readings",
                        "GET",
                        200,
                        0,
                        5,
                        70,
                        20
                    ),
                    new TrafficMetricBucket(
                        currentBucketStart - TimeSpan.FromHours(49).Ticks,
                        "/api/readings",
                        "GET",
                        200,
                        0,
                        99,
                        990,
                        20
                    ),
                ]
            );
            var service = new TrafficAnalyticsService(store, new TestCurrentUser());

            var summary = await service.GetSummaryAsync("24h", CancellationToken.None);

            summary.TotalRequests.Should().Be(4);
            summary.PreviousPeriodRequests.Should().Be(5);
            summary.RequestChangePercent.Should().Be(-20);
            summary.SuccessfulResponses.Should().Be(2);
            summary.ClientErrorResponses.Should().Be(0);
            summary.RateLimitedResponses.Should().Be(1);
            summary.ServerErrorResponses.Should().Be(1);
            summary.P50LatencyBand.Should().Be(0);
            summary.P95LatencyBand.Should().Be(7);
            summary.Timeline.Should().HaveCount(24);
            summary.TopRoutes.Should().ContainSingle(route => route.RouteTemplate == "/api/readings");
            summary.TopStatuses.Should().Contain(status => status.StatusCode == 429 && status.RequestCount == 1);
        }

        [Fact]
        public async Task Summary_rejects_members()
        {
            var service = new TrafficAnalyticsService(
                new TestDataStore(role: AllowedUserRole.Member),
                new TestCurrentUser()
            );

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.GetSummaryAsync("24h", CancellationToken.None)
            );
        }
    }
}
