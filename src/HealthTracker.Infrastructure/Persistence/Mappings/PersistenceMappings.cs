using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence.Models;

namespace HealthTracker.Infrastructure.Persistence.Mappings
{
    public static class PersistenceMappings
    {
        public static ApplicationUser ToDomain(this UserRecord record)
        {
            return new()
            {
                Id = record.Id,
                Subject = record.Subject,
                DisplayName = record.DisplayName,
                CreatedUtc = record.CreatedUtc,
            };
        }

        public static UserRecord ToRecord(this ApplicationUser user)
        {
            return new()
            {
                Id = user.Id,
                Subject = user.Subject,
                DisplayName = user.DisplayName,
                CreatedUtc = user.CreatedUtc,
            };
        }

        public static MeasurementTemplate ToDomain(this TemplateRecord record)
        {
            return new()
            {
                Id = record.Id,
                OwnerUserId = record.OwnerUserId,
                Code = record.Code,
                Name = record.Name,
                Category = record.Category,
                UnitCategory = record.UnitCategory,
                NormalizedUnit = record.NormalizedUnit,
                AllowedUnits = record.AllowedUnits.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static TemplateRecord ToRecord(this MeasurementTemplate template)
        {
            return new()
            {
                Id = template.Id,
                OwnerUserId = template.OwnerUserId,
                Code = template.Code,
                Name = template.Name,
                Category = template.Category,
                UnitCategory = template.UnitCategory,
                NormalizedUnit = template.NormalizedUnit,
                AllowedUnits = string.Join('|', template.AllowedUnits),
                CreatedUtc = template.CreatedUtc,
                DeletedUtc = template.DeletedUtc,
            };
        }

        public static void Apply(this MeasurementTemplate source, TemplateRecord target)
        {
            target.Name = source.Name;
            target.Category = source.Category;
            target.UnitCategory = source.UnitCategory;
            target.NormalizedUnit = source.NormalizedUnit;
            target.AllowedUnits = string.Join('|', source.AllowedUnits);
            target.DeletedUtc = source.DeletedUtc;
        }

        public static UserTrackedTemplate ToDomain(this TrackedTemplateRecord record)
        {
            return new()
            {
                Id = record.Id,
                UserId = record.UserId,
                TemplateId = record.TemplateId,
                Template = record.Template.ToDomain(),
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static TrackedTemplateRecord ToRecord(this UserTrackedTemplate tracking)
        {
            return new()
            {
                Id = tracking.Id,
                UserId = tracking.UserId,
                TemplateId = tracking.TemplateId,
                CreatedUtc = tracking.CreatedUtc,
                DeletedUtc = tracking.DeletedUtc,
            };
        }

        public static void Apply(this UserTrackedTemplate source, TrackedTemplateRecord target)
        {
            target.DeletedUtc = source.DeletedUtc;
        }

        public static HealthReading ToDomain(this ReadingRecord record)
        {
            return new()
            {
                Id = record.Id,
                UserId = record.UserId,
                TemplateId = record.TemplateId,
                TemplateName = record.Template.Name,
                Value = record.Value,
                Unit = record.Unit,
                Note = record.Note,
                RecordedAtUtc = record.RecordedAtUtc,
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static ReadingRecord ToRecord(this HealthReading reading)
        {
            return new()
            {
                Id = reading.Id,
                UserId = reading.UserId,
                TemplateId = reading.TemplateId,
                Value = reading.Value,
                Unit = reading.Unit,
                Note = reading.Note,
                RecordedAtUtc = reading.RecordedAtUtc,
                CreatedUtc = reading.CreatedUtc,
                DeletedUtc = reading.DeletedUtc,
            };
        }

        public static void Apply(this HealthReading source, ReadingRecord target)
        {
            target.Value = source.Value;
            target.Unit = source.Unit;
            target.Note = source.Note;
            target.RecordedAtUtc = source.RecordedAtUtc;
            target.DeletedUtc = source.DeletedUtc;
        }
    }
}
