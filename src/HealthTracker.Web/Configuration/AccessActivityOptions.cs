namespace HealthTracker.Web.Configuration
{
    /// <summary>
    /// The Docker network CIDRs from which the application accepts Cloudflare Tunnel forwarding
    /// headers. Leaving this empty is safe: the connection IP is used and client headers are ignored.
    /// </summary>
    public sealed class AccessActivityOptions
    {
        public const string SectionName = "AccessActivity";
        public string[] TrustedProxyNetworks { get; init; } = [];
    }
}
