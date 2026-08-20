namespace HealthTracker.Domain.Models
{
    public sealed class UserTrackedTemplate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId
        {
            get; set;
        }
        public Guid TemplateId
        {
            get; set;
        }
        public MeasurementTemplate Template { get; set; } = new();
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedUtc
        {
            get; set;
        }
    }
}
