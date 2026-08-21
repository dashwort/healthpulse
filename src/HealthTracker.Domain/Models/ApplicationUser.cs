namespace HealthTracker.Domain.Models
{
    public sealed class ApplicationUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Subject { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum AllowedUserRole
    {
        Member,
        Admin,
    }

    public sealed class AllowedUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public AllowedUserRole Role
        {
            get; set;
        }
        public Guid? ApplicationUserId
        {
            get; set;
        }
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FirstSignedInUtc
        {
            get; set;
        }
        public DateTimeOffset? LastSignedInUtc
        {
            get; set;
        }
        public DateTimeOffset? DeletedUtc
        {
            get; set;
        }
    }

    public sealed class PersonalAccessToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AllowedUserId
        {
            get; set;
        }
        public string Name { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ExpiresUtc
        {
            get; set;
        }
        public DateTimeOffset? LastUsedUtc
        {
            get; set;
        }
        public DateTimeOffset? RevokedUtc
        {
            get; set;
        }
    }

    public sealed class McpAuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PersonalAccessTokenId
        {
            get; set;
        }
        public Guid AllowedUserId
        {
            get; set;
        }
        public string Method { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
