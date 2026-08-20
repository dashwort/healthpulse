using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Tests
{
    public sealed class HealthTrackerServiceTests
    {
        [Fact]
        public async Task Create_reading_preserves_a_short_note()
        {
            var store = new FakeStore();
            var template = BuiltInTemplates.All.Single(x => x.Code == "glucose");
            store.Templates.Add(template);
            store.Trackings.Add(
                new UserTrackedTemplate
                {
                    UserId = store.User.Id,
                    TemplateId = template.Id,
                    Template = template,
                }
            );
            var service = new HealthTrackerService(store, new FakeCurrentUser());

            var reading = await service.CreateReadingAsync(
                new CreateReadingDto(template.Id, 100m, "mg/dL", DateTimeOffset.UtcNow, "Fasting"),
                CancellationToken.None
            );

            Assert.Equal("Fasting", reading.Note);
            Assert.Equal("mmol/L", reading.Unit);
        }

        [Fact]
        public async Task Create_reading_rejects_a_note_over_140_characters()
        {
            var store = new FakeStore();
            var template = BuiltInTemplates.All.Single(x => x.Code == "glucose");
            store.Templates.Add(template);
            store.Trackings.Add(
                new UserTrackedTemplate
                {
                    UserId = store.User.Id,
                    TemplateId = template.Id,
                    Template = template,
                }
            );
            var service = new HealthTrackerService(store, new FakeCurrentUser());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateReadingAsync(
                    new CreateReadingDto(
                        template.Id,
                        5m,
                        "mmol/L",
                        DateTimeOffset.UtcNow,
                        new string('x', 141)
                    ),
                    CancellationToken.None
                )
            );
        }

        [Fact]
        public async Task Reading_page_filters_and_reports_total_count()
        {
            var store = new FakeStore();
            var template = BuiltInTemplates.All.Single(x => x.Code == "glucose");
            store.Templates.Add(template);
            store.Readings.AddRange(
                Enumerable
                    .Range(1, 3)
                    .Select(i => new HealthReading
                    {
                        UserId = store.User.Id,
                        TemplateId = template.Id,
                        TemplateName = template.Name,
                        Value = i,
                        Unit = "mmol/L",
                        RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-i),
                    })
            );
            var service = new HealthTrackerService(store, new FakeCurrentUser());

            var page = await service.GetReadingPageAsync(
                template.Id,
                null,
                null,
                2,
                2,
                CancellationToken.None
            );

            Assert.Equal(3, page.TotalCount);
            Assert.Single(page.Items);
        }

        private sealed class FakeCurrentUser : ICurrentUser
        {
            public string Subject => "test-user";
            public string DisplayName => "Test user";
        }

        private sealed class FakeStore : IHealthDataStore
        {
            public ApplicationUser User
            {
                get;
            } =
                new()
                {
                    Subject = "test-user",
                    DisplayName = "Test user"
                };
            public List<MeasurementTemplate> Templates { get; } = [];
            public List<UserTrackedTemplate> Trackings { get; } = [];
            public List<HealthReading> Readings { get; } = [];

            public Task<ApplicationUser?> FindUserBySubjectAsync(
                string subject,
                CancellationToken ct
            )
            {
                return Task.FromResult<ApplicationUser?>(subject == User.Subject ? User : null);
            }

            public Task AddUserAsync(ApplicationUser user, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
                CancellationToken ct
            )
            {
                return Task.FromResult<IReadOnlyCollection<MeasurementTemplate>>(Templates);
            }

            public Task<MeasurementTemplate?> GetTemplateForUserAsync(
                Guid userId,
                Guid templateId,
                bool includeDeleted,
                CancellationToken ct
            )
            {
                return Task.FromResult(Templates.SingleOrDefault(x => x.Id == templateId));
            }

            public Task<IReadOnlyCollection<UserTrackedTemplate>> GetTrackedTemplatesAsync(
                Guid userId,
                CancellationToken ct
            )
            {
                return Task.FromResult<IReadOnlyCollection<UserTrackedTemplate>>([
                    .. Trackings.Where(x => x.UserId == userId && x.DeletedUtc is null),
                ]);
            }

            public Task<UserTrackedTemplate?> GetTrackingAsync(
                Guid userId,
                Guid templateId,
                bool includeDeleted,
                CancellationToken ct
            )
            {
                return Task.FromResult(
                    Trackings.SingleOrDefault(x =>
                        x.UserId == userId
                        && x.TemplateId == templateId
                        && (includeDeleted || x.DeletedUtc is null)
                    )
                );
            }

            public Task AddTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct)
            {
                Trackings.Add(tracking);
                return Task.CompletedTask;
            }

            public Task UpdateTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task AddTemplateAsync(MeasurementTemplate template, CancellationToken ct)
            {
                Templates.Add(template);
                return Task.CompletedTask;
            }

            public Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task AddReadingAsync(HealthReading reading, CancellationToken ct)
            {
                Readings.Add(reading);
                return Task.CompletedTask;
            }

            public Task<HealthReading?> GetReadingAsync(
                Guid userId,
                Guid readingId,
                bool includeDeleted,
                CancellationToken ct
            )
            {
                return Task.FromResult(
                    Readings.SingleOrDefault(x =>
                        x.UserId == userId
                        && x.Id == readingId
                        && (includeDeleted || x.DeletedUtc is null)
                    )
                );
            }

            public Task<IReadOnlyCollection<HealthReading>> GetReadingsAsync(
                Guid userId,
                Guid? templateId,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                CancellationToken ct
            )
            {
                return Task.FromResult<IReadOnlyCollection<HealthReading>>([
                    .. Readings
                        .Where(x =>
                            x.UserId == userId
                            && x.DeletedUtc is null
                            && (!templateId.HasValue || x.TemplateId == templateId)
                            && (!fromUtc.HasValue || x.RecordedAtUtc >= fromUtc)
                            && (!toUtc.HasValue || x.RecordedAtUtc <= toUtc)
                        )
                        .OrderByDescending(x => x.RecordedAtUtc),
                ]);
            }

            public Task UpdateReadingAsync(HealthReading reading, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task<int> PurgeSoftDeletedAsync(DateTimeOffset beforeUtc, CancellationToken ct)
            {
                return Task.FromResult(0);
            }

            public Task SaveChangesAsync(CancellationToken ct)
            {
                return Task.CompletedTask;
            }
        }
    }
}
