namespace HealthTracker.Domain.Models
{
    /// <summary>
    /// A privacy-minimizing request histogram cell. It contains route templates and counters only;
    /// individual paths, identities, IP addresses, and request content are never retained here.
    /// </summary>
    public sealed record TrafficMetricBucket(
        long BucketStartUtcTicks,
        string RouteTemplate,
        string Method,
        int StatusCode,
        int LatencyBand,
        long RequestCount,
        long TotalElapsedMilliseconds,
        long MaxElapsedMilliseconds
    );
}
