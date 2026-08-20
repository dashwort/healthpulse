using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Application.Mappings;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    public sealed class HealthTrackerService(IHealthDataStore dataStore, ICurrentUser currentUser)
    {
        public async Task<IReadOnlyCollection<TemplateDto>> GetCatalogueAsync(CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var trackedIds = (await dataStore.GetTrackedTemplatesAsync(user.Id, ct))
                .Select(x => x.TemplateId)
                .ToHashSet();
            return
            [
                .. (await dataStore.GetCatalogueAsync(ct))
                    .Select(x => x.ToDto(trackedIds.Contains(x.Id)))
                    .OrderBy(x => x.Category)
                    .ThenBy(x => x.Name),
            ];
        }

        public async Task<IReadOnlyCollection<TemplateDto>> GetTrackedTemplatesAsync(
            CancellationToken ct
        )
        {
            var user = await GetCurrentUserAsync(ct);
            return
            [
                .. (await dataStore.GetTrackedTemplatesAsync(user.Id, ct))
                    .Select(x => x.Template.ToDto(true))
                    .OrderBy(x => x.Category)
                    .ThenBy(x => x.Name),
            ];
        }

        public async Task<TemplateDto> CreateCustomTemplateAsync(
            CreateCustomTemplateDto request,
            CancellationToken ct
        )
        {
            ValidateTemplate(request.Name, request.Category, request.Unit);
            var user = await GetCurrentUserAsync(ct);
            var template = new MeasurementTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = user.Id,
                Name = request.Name.Trim(),
                Category = request.Category.Trim(),
                NormalizedUnit = request.Unit.Trim(),
                AllowedUnits = [request.Unit.Trim()],
                UnitCategory = "None",
            };
            await dataStore.AddTemplateAsync(template, ct);
            await dataStore.AddTrackingAsync(
                new UserTrackedTemplate
                {
                    UserId = user.Id,
                    TemplateId = template.Id,
                    Template = template,
                },
                ct
            );
            await dataStore.SaveChangesAsync(ct);
            return template.ToDto(true);
        }

        public async Task<TemplateDto> UpdateCustomTemplateAsync(
            Guid templateId,
            UpdateCustomTemplateDto request,
            CancellationToken ct
        )
        {
            ValidateTemplate(request.Name, request.Category, request.Unit);
            var user = await GetCurrentUserAsync(ct);
            var template = await RequireCustomTemplateAsync(user.Id, templateId, ct);
            template.Name = request.Name.Trim();
            template.Category = request.Category.Trim();
            template.NormalizedUnit = request.Unit.Trim();
            template.AllowedUnits = [request.Unit.Trim()];
            await dataStore.UpdateTemplateAsync(template, ct);
            await dataStore.SaveChangesAsync(ct);
            return template.ToDto(true);
        }

        public async Task TrackTemplateAsync(Guid templateId, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var template =
                await dataStore.GetTemplateForUserAsync(user.Id, templateId, false, ct)
                ?? throw new KeyNotFoundException("Template not found.");
            var tracking = await dataStore.GetTrackingAsync(user.Id, template.Id, true, ct);
            if (tracking is null)
            {
                await dataStore.AddTrackingAsync(
                    new UserTrackedTemplate
                    {
                        UserId = user.Id,
                        TemplateId = template.Id,
                        Template = template,
                    },
                    ct
                );
            }
            else
            {
                tracking.DeletedUtc = null;
                await dataStore.UpdateTrackingAsync(tracking, ct);
            }
            await dataStore.SaveChangesAsync(ct);
        }

        public async Task StopTrackingAsync(Guid templateId, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var tracking =
                await dataStore.GetTrackingAsync(user.Id, templateId, false, ct)
                ?? throw new KeyNotFoundException("Tracked template not found.");
            tracking.DeletedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateTrackingAsync(tracking, ct);
            await dataStore.SaveChangesAsync(ct);
        }

        public async Task DeleteCustomTemplateAsync(Guid templateId, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var template = await RequireCustomTemplateAsync(user.Id, templateId, ct);
            template.DeletedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateTemplateAsync(template, ct);
            var tracking = await dataStore.GetTrackingAsync(user.Id, templateId, false, ct);
            if (tracking is not null)
            {
                tracking.DeletedUtc = template.DeletedUtc;
                await dataStore.UpdateTrackingAsync(tracking, ct);
            }
            await dataStore.SaveChangesAsync(ct);
        }

        public async Task<ReadingDto> CreateReadingAsync(CreateReadingDto request, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var template = await RequireTrackedTemplateAsync(user.Id, request.TemplateId, ct);
            ValidateReading(request.Value, request.Unit, request.RecordedAtUtc);
            var reading = new HealthReading
            {
                UserId = user.Id,
                TemplateId = template.Id,
                TemplateName = template.Name,
                Value = template.IsCustom
                    ? request.Value
                    : UnitConverter.Normalize(request.Value, template, request.Unit.Trim()),
                Unit = template.IsCustom ? request.Unit.Trim() : template.NormalizedUnit,
                Note = NormalizeNote(request.Note),
                RecordedAtUtc = request.RecordedAtUtc.ToUniversalTime(),
            };
            await dataStore.AddReadingAsync(reading, ct);
            await dataStore.SaveChangesAsync(ct);
            return reading.ToDto();
        }

        public async Task<IReadOnlyCollection<ReadingDto>> GetReadingsAsync(
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            CancellationToken ct
        )
        {
            var user = await GetCurrentUserAsync(ct);
            return
            [
                .. (await dataStore.GetReadingsAsync(user.Id, templateId, fromUtc, toUtc, ct)).Select(
                    x => x.ToDto()
                ),
            ];
        }

        public async Task<ReadingPageDto> GetReadingPageAsync(
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int page,
            int pageSize,
            CancellationToken ct
        )
        {
            if (page < 1 || pageSize is < 1 or > 100)
            {
                throw new InvalidOperationException("The requested page is invalid.");
            }

            var readings = await GetReadingsAsync(templateId, fromUtc, toUtc, ct);
            return new ReadingPageDto(
                [.. readings.Skip((page - 1) * pageSize).Take(pageSize)],
                readings.Count,
                page,
                pageSize
            );
        }

        public async Task<IReadOnlyCollection<MetricSummaryDto>> GetMetricSummariesAsync(
            CancellationToken ct
        )
        {
            var tracked = await GetTrackedTemplatesAsync(ct);
            var readings = await GetReadingsAsync(null, null, null, ct);
            return
            [
                .. tracked
                    .Select(template =>
                    {
                        var values = readings
                            .Where(x => x.TemplateId == template.Id)
                            .OrderByDescending(x => x.RecordedAtUtc)
                            .ToArray();
                        if (values.Length == 0)
                        {
                            return null;
                        }

                        return new MetricSummaryDto(
                            template.Id,
                            template.Name,
                            values[0].Value,
                            values[0].Unit,
                            values[0].RecordedAtUtc,
                            values.Length > 1 ? values[0].Value - values[1].Value : null
                        );
                    })
                    .OfType<MetricSummaryDto>(),
            ];
        }

        public async Task<ReadingDto> UpdateReadingAsync(
            Guid readingId,
            UpdateReadingDto request,
            CancellationToken ct
        )
        {
            var user = await GetCurrentUserAsync(ct);
            var reading =
                await dataStore.GetReadingAsync(user.Id, readingId, false, ct)
                ?? throw new KeyNotFoundException("Reading not found.");
            var template = await RequireTrackedTemplateAsync(user.Id, reading.TemplateId, ct);
            ValidateReading(request.Value, request.Unit, request.RecordedAtUtc);
            reading.Value = template.IsCustom
                ? request.Value
                : UnitConverter.Normalize(request.Value, template, request.Unit.Trim());
            reading.Unit = template.IsCustom ? request.Unit.Trim() : template.NormalizedUnit;
            reading.Note = NormalizeNote(request.Note);
            reading.RecordedAtUtc = request.RecordedAtUtc.ToUniversalTime();
            await dataStore.UpdateReadingAsync(reading, ct);
            await dataStore.SaveChangesAsync(ct);
            return reading.ToDto();
        }

        public async Task DeleteReadingAsync(Guid readingId, CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var reading =
                await dataStore.GetReadingAsync(user.Id, readingId, false, ct)
                ?? throw new KeyNotFoundException("Reading not found.");
            reading.DeletedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateReadingAsync(reading, ct);
            await dataStore.SaveChangesAsync(ct);
        }

        private async Task<ApplicationUser> GetCurrentUserAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(currentUser.Subject))
            {
                throw new UnauthorizedAccessException();
            }

            var user = await dataStore.FindUserBySubjectAsync(currentUser.Subject, ct);
            if (user is not null)
            {
                return user;
            }

            user = new ApplicationUser
            {
                Subject = currentUser.Subject,
                DisplayName = currentUser.DisplayName,
            };
            await dataStore.AddUserAsync(user, ct);
            await dataStore.SaveChangesAsync(ct);
            return user;
        }

        private async Task<MeasurementTemplate> RequireCustomTemplateAsync(
            Guid userId,
            Guid templateId,
            CancellationToken ct
        )
        {
            return await dataStore.GetTemplateForUserAsync(userId, templateId, false, ct)
                is { IsCustom: true } template
                ? template
                : throw new KeyNotFoundException("Custom template not found.");
        }

        private async Task<MeasurementTemplate> RequireTrackedTemplateAsync(
            Guid userId,
            Guid templateId,
            CancellationToken ct
        )
        {
            return (await dataStore.GetTrackingAsync(userId, templateId, false, ct))?.Template
            ?? throw new KeyNotFoundException("Tracked template not found.");
        }

        private static void ValidateTemplate(string name, string category, string unit)
        {
            if (
                string.IsNullOrWhiteSpace(name)
                || name.Trim().Length > 100
                || string.IsNullOrWhiteSpace(category)
                || category.Trim().Length > 100
                || string.IsNullOrWhiteSpace(unit)
                || unit.Trim().Length > 30
            )
            {
                throw new InvalidOperationException(
                    "A name, category, and unit within the allowed lengths are required."
                );
            }
        }

        private static void ValidateReading(decimal value, string unit, DateTimeOffset recordedAtUtc)
        {
            if (
                value < 0
                || value > 1_000_000
                || string.IsNullOrWhiteSpace(unit)
                || recordedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5)
            )
            {
                throw new InvalidOperationException(
                    "The reading value, unit, or timestamp is invalid."
                );
            }
        }

        private static string? NormalizeNote(string? note)
        {
            var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (normalized?.Length > 140)
            {
                throw new InvalidOperationException("A reading note cannot exceed 140 characters.");
            }

            return normalized;
        }
    }
}
