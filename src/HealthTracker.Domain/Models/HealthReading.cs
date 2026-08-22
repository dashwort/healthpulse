namespace HealthTracker.Domain.Models
{
    public sealed class HealthReading
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTimeOffset RecordedAtUtc { get; set; }
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedUtc { get; set; }
    }
}
