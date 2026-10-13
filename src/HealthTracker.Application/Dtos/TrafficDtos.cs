namespace HealthTracker.Application.Dtos
{
    public sealed record TrafficSummaryDto(
        string Range,
        DateTimeOffset FromUtc,
        DateTimeOffset ToUtc,
        long TotalRequests,
        long PreviousPeriodRequests,
        double? RequestChangePercent,
        long SuccessfulResponses,
        long ClientErrorResponses,
        long RateLimitedResponses,
        long ServerErrorResponses,
        int P50LatencyBand,
        int P95LatencyBand,
        IReadOnlyCollection<TrafficTimelineBucketDto> Timeline,
        IReadOnlyCollection<TrafficRouteSummaryDto> TopRoutes,
        IReadOnlyCollection<TrafficStatusSummaryDto> TopStatuses
    );

    public sealed record TrafficTimelineBucketDto(
        DateTimeOffset StartUtc,
        long SuccessfulResponses,
        long ClientErrorResponses,
        long RateLimitedResponses,
        long ServerErrorResponses
    );

    public sealed record TrafficRouteSummaryDto(
        string RouteTemplate,
        string Method,
        long RequestCount,
        long ErrorCount,
        int P95LatencyBand
    );

    public sealed record TrafficStatusSummaryDto(int StatusCode, long RequestCount);
}
