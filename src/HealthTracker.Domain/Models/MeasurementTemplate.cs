namespace HealthTracker.Domain.Models
{
    public sealed class MeasurementTemplate
    {
        public Guid Id { get; set; }
        public Guid? OwnerUserId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UnitCategory { get; set; } = "None";
        public string NormalizedUnit { get; set; } = string.Empty;
        public IReadOnlyCollection<string> AllowedUnits { get; set; } = [];
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeletedUtc { get; set; }
        public bool IsCustom => OwnerUserId.HasValue;
    }
}
