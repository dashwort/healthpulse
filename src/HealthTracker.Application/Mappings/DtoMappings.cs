using HealthTracker.Application.Dtos;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Mappings
{
    public static class DtoMappings
    {
        public static TemplateDto ToDto(this MeasurementTemplate template, bool isTracked = false)
        {
            return new(
                template.Id,
                template.Code,
                template.Name,
                template.Category,
                template.NormalizedUnit,
                template.AllowedUnits,
                template.IsCustom,
                isTracked
            );
        }

        public static ReadingDto ToDto(this HealthReading reading)
        {
            return new(
                reading.Id,
                reading.TemplateId,
                reading.TemplateName,
                reading.Value,
                reading.Unit,
                reading.RecordedAtUtc,
                reading.Note
            );
        }
    }
}
