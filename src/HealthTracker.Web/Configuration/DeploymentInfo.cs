namespace HealthTracker.Web.Configuration
{
    public sealed class DeploymentInfo
    {
        public const string SectionName = "Deployment";

        public string Version { get; set; } = "development";

        public string Build { get; set; } = "local";

        public string Commit { get; set; } = "local";

        public string BuiltAtUtc { get; set; } = "Not available";
    }
}
