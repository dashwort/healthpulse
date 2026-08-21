namespace HealthTracker.Infrastructure.Persistence.Models
{
    public sealed class UserRecord
    {
        public Guid Id
        {
            get; set;
        }
        public string Subject { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc
        {
            get; set;
        }
    }

    public sealed class AllowedUserRecord
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? ApplicationUserId { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset? FirstSignedInUtc { get; set; }
        public DateTimeOffset? LastSignedInUtc { get; set; }
        public DateTimeOffset? DeletedUtc { get; set; }
    }

    public sealed class PersonalAccessTokenRecord
    {
        public Guid Id { get; set; }
        public Guid AllowedUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset ExpiresUtc { get; set; }
        public DateTimeOffset? LastUsedUtc { get; set; }
        public DateTimeOffset? RevokedUtc { get; set; }
    }

    public sealed class McpAuditLogRecord
    {
        public Guid Id { get; set; }
        public Guid PersonalAccessTokenId { get; set; }
        public Guid AllowedUserId { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public DateTimeOffset OccurredUtc { get; set; }
    }

    public sealed class TemplateRecord
    {
        public Guid Id
        {
            get; set;
        }
        public Guid? OwnerUserId
        {
            get; set;
        }
        public string? Code
        {
            get; set;
        }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UnitCategory { get; set; } = string.Empty;
        public string NormalizedUnit { get; set; } = string.Empty;
        public string AllowedUnits { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc
        {
            get; set;
        }
        public DateTimeOffset? DeletedUtc
        {
            get; set;
        }
    }

    public sealed class TrackedTemplateRecord
    {
        public Guid Id
        {
            get; set;
        }
        public Guid UserId
        {
            get; set;
        }
        public Guid TemplateId
        {
            get; set;
        }
        public TemplateRecord Template { get; set; } = null!;
        public DateTimeOffset CreatedUtc
        {
            get; set;
        }
        public DateTimeOffset? DeletedUtc
        {
            get; set;
        }
    }

    public sealed class ReadingRecord
    {
        public Guid Id
        {
            get; set;
        }
        public Guid UserId
        {
            get; set;
        }
        public Guid TemplateId
        {
            get; set;
        }
        public TemplateRecord Template { get; set; } = null!;
        public decimal Value
        {
            get; set;
        }
        public string Unit { get; set; } = string.Empty;
        public string? Note
        {
            get; set;
        }
        public DateTimeOffset RecordedAtUtc
        {
            get; set;
        }
        public DateTimeOffset CreatedUtc
        {
            get; set;
        }
        public DateTimeOffset? DeletedUtc
        {
            get; set;
        }
    }
}
