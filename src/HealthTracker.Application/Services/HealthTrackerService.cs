using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Application.Mappings;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    public sealed class HealthTrackerService(IHealthDataStore dataStore, ICurrentUser currentUser)
    {
        private static readonly SemaphoreSlim AdministratorMutationLock = new(1, 1);

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
        {
            return dataStore.ExecuteInTransactionAsync(operation, ct);
        }
        public async Task<IReadOnlyCollection<AllowedUserDto>> GetAllowedUsersAsync(
            bool includeArchived,
            CancellationToken ct
        )
        {
            await RequireAdministratorAsync(ct);
            return [
                .. (await dataStore.GetAllowedUsersAsync(includeArchived, ct)).Select(ToAllowedUserDto),
            ];
        }

        public async Task<AllowedUserDto> AddAllowedUserAsync(
            AddAllowedUserDto request,
            CancellationToken ct
        )
        {
            await RequireAdministratorAsync(ct);
            var (email, normalizedEmail) = NormalizeEmail(request.Email);
            var role = ParseRole(request.Role);
            var existing = await dataStore.FindAllowedUserByEmailAsync(normalizedEmail, true, ct);
            if (existing is null)
            {
                existing = new AllowedUser
                {
                    Email = email,
                    NormalizedEmail = normalizedEmail,
                    Role = role,
                };
                await dataStore.AddAllowedUserAsync(existing, ct);
            }
            else
            {
                existing.Email = email;
                existing.Role = role;
                existing.DeletedUtc = null;
                await dataStore.UpdateAllowedUserAsync(existing, ct);
            }

            await dataStore.SaveChangesAsync(ct);
            return ToAllowedUserDto(existing);
        }

        public async Task<AllowedUserDto> UpdateAllowedUserRoleAsync(
            Guid allowedUserId,
            UpdateAllowedUserRoleDto request,
            CancellationToken ct
        )
        {
            await AdministratorMutationLock.WaitAsync(ct);
            try
            {
                return await dataStore.ExecuteInTransactionAsync(async () =>
                {
                    await RequireAdministratorAsync(ct);
                    var allowedUser = await RequireAllowedUserAsync(allowedUserId, ct);
                    var role = ParseRole(request.Role);
                    if (allowedUser.Role == AllowedUserRole.Admin && role != AllowedUserRole.Admin)
                    {
                        await EnsureNotLastAdministratorAsync(ct);
                    }

                    allowedUser.Role = role;
                    await dataStore.UpdateAllowedUserAsync(allowedUser, ct);
                    await dataStore.SaveChangesAsync(ct);
                    return ToAllowedUserDto(allowedUser);
                }, ct);
            }
            finally
            {
                AdministratorMutationLock.Release();
            }
        }

        public async Task ArchiveAllowedUserAsync(Guid allowedUserId, CancellationToken ct)
        {
            await AdministratorMutationLock.WaitAsync(ct);
            try
            {
                await dataStore.ExecuteInTransactionAsync(async () =>
                {
                    await RequireAdministratorAsync(ct);
                    var allowedUser = await RequireAllowedUserAsync(allowedUserId, ct);
                    if (allowedUser.Role == AllowedUserRole.Admin)
                    {
                        await EnsureNotLastAdministratorAsync(ct);
                    }

                    allowedUser.DeletedUtc = DateTimeOffset.UtcNow;
                    await dataStore.UpdateAllowedUserAsync(allowedUser, ct);
                    foreach (var token in await dataStore.GetTokensAsync(allowedUser.Id, ct))
                    {
                        if (!token.RevokedUtc.HasValue)
                        {
                            token.RevokedUtc = DateTimeOffset.UtcNow;
                            await dataStore.UpdateTokenAsync(token, ct);
                        }
                    }

                    await dataStore.SaveChangesAsync(ct);
                    return true;
                }, ct);
            }
            finally
            {
                AdministratorMutationLock.Release();
            }
        }

        public async Task<IReadOnlyCollection<TemplateDto>> GetCatalogueAsync(CancellationToken ct)
        {
            var user = await GetCurrentUserAsync(ct);
            var trackedIds = (await dataStore.GetTrackedTemplatesAsync(user.Id, ct))
                .Select(x => x.TemplateId)
                .ToHashSet();
            return
            [
                .. (await dataStore.GetCatalogueAsync(user.Id, ct))
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

            var user = await GetCurrentUserAsync(ct);
            var (readings, totalCount) = await dataStore.GetReadingsPageAsync(
                user.Id,
                templateId,
                fromUtc,
                toUtc,
                page,
                pageSize,
                ct
            );
            return new ReadingPageDto(
                [.. readings.Select(x => x.ToDto())],
                totalCount,
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
            if (string.IsNullOrWhiteSpace(currentUser.Subject) || string.IsNullOrWhiteSpace(currentUser.Email))
            {
                throw new UnauthorizedAccessException();
            }

            var allowedUser = await dataStore.FindAllowedUserByEmailAsync(
                NormalizeEmail(currentUser.Email).NormalizedEmail,
                false,
                ct
            );
            if (allowedUser is null)
            {
                throw new UnauthorizedAccessException();
            }

            var user = await dataStore.FindUserBySubjectAsync(currentUser.Subject, ct);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Subject = currentUser.Subject,
                    DisplayName = currentUser.DisplayName,
                };
                await dataStore.AddUserAsync(user, ct);
            }

            // Approved people can have signed in before the allow-list feature existed.
            // Always link the current subject, rather than only linking newly-created users.
            allowedUser.ApplicationUserId = user.Id;
            allowedUser.FirstSignedInUtc ??= DateTimeOffset.UtcNow;

            allowedUser.LastSignedInUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateAllowedUserAsync(allowedUser, ct);
            await dataStore.SaveChangesAsync(ct);
            return user;
        }

        private async Task<AllowedUser> RequireAdministratorAsync(CancellationToken ct)
        {
            await GetCurrentUserAsync(ct);
            var allowedUser = await dataStore.FindAllowedUserByEmailAsync(
                NormalizeEmail(currentUser.Email).NormalizedEmail,
                false,
                ct
            );
            return allowedUser is { Role: AllowedUserRole.Admin }
                ? allowedUser
                : throw new UnauthorizedAccessException();
        }

        private async Task<AllowedUser> RequireAllowedUserAsync(Guid allowedUserId, CancellationToken ct)
        {
            return (await dataStore.GetAllowedUsersAsync(true, ct)).SingleOrDefault(x => x.Id == allowedUserId)
                ?? throw new KeyNotFoundException("User not found.");
        }

        private async Task EnsureNotLastAdministratorAsync(CancellationToken ct)
        {
            if (await dataStore.CountActiveAdministratorsAsync(ct) <= 1)
            {
                throw new InvalidOperationException("At least one active administrator is required.");
            }
        }

        private static AllowedUserDto ToAllowedUserDto(AllowedUser user)
        {
            return new(
                user.Id,
                user.Email,
                user.Role.ToString(),
                user.FirstSignedInUtc.HasValue,
                user.FirstSignedInUtc,
                user.LastSignedInUtc,
                user.DeletedUtc.HasValue
            );
        }

        private static (string Email, string NormalizedEmail) NormalizeEmail(string email)
        {
            var trimmed = email?.Trim() ?? string.Empty;
            if (
                trimmed.Length is 0 or > 320
                || !System.Net.Mail.MailAddress.TryCreate(trimmed, out _)
            )
            {
                throw new InvalidOperationException("A valid email address is required.");
            }

            return (trimmed, trimmed.ToUpperInvariant());
        }

        private static AllowedUserRole ParseRole(string role)
        {
            return Enum.TryParse<AllowedUserRole>(role, true, out var parsed)
                ? parsed
                : throw new InvalidOperationException("The user role is invalid.");
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
