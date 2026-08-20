namespace HealthTracker.Web.Configuration
{
    public sealed class ExternalOidcSettings
    {
        public const string SectionName = "Authentication:OpenIdConnect";
        public string Authority { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string CallbackPath { get; init; } = "/signin-oidc";
        public string[] Scopes { get; init; } = ["openid", "profile", "email"];
    }
}
