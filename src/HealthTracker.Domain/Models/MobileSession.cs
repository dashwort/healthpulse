namespace HealthTracker.Domain.Models
{
    /// <summary>
    /// A revocable Android device session. Only SHA-256 token hashes are stored.
    /// </summary>
    public sealed class MobileSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationUserId { get; set; }
        public string AccessTokenHash { get; set; } = string.Empty;
        public DateTimeOffset AccessTokenExpiresUtc { get; set; }
        public string RefreshTokenHash { get; set; } = string.Empty;
        public DateTimeOffset RefreshTokenExpiresUtc { get; set; }
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastUsedUtc { get; set; }
        public DateTimeOffset? RevokedUtc { get; set; }
    }
}
