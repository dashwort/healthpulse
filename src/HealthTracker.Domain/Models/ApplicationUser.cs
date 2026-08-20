namespace HealthTracker.Domain.Models
{
    public sealed class ApplicationUser
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Subject { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
