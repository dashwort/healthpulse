namespace HealthTracker.Domain.Models
{
    /// <summary>
    /// A short-lived, PKCE-protected hand-off between the system browser and the Android app.
    /// The authorization code itself is never persisted.
    /// </summary>
    public sealed class MobileAuthorizationRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CodeChallenge { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ExpiresUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);
        public Guid? ApplicationUserId { get; set; }
        public string? AuthorizationCodeHash { get; set; }
        public DateTimeOffset? AuthorizationCodeExpiresUtc { get; set; }
        public DateTimeOffset? ConsumedUtc { get; set; }
    }
}
