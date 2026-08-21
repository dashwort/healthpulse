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

        [Fact]
        public async Task Catalogue_excludes_custom_templates_owned_by_another_user()
        {
            var store = new FakeStore();
            var ownTemplate = new MeasurementTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = store.User.Id,
                Name = "Own metric",
                Category = "Custom",
                NormalizedUnit = "unit",
                AllowedUnits = ["unit"],
            };
            var otherTemplate = new MeasurementTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = Guid.NewGuid(),
                Name = "Private metric",
                Category = "Custom",
                NormalizedUnit = "unit",
                AllowedUnits = ["unit"],
            };
            store.Templates.AddRange([ownTemplate, otherTemplate]);
            var service = new HealthTrackerService(store, new FakeCurrentUser());

            var catalogue = await service.GetCatalogueAsync(CancellationToken.None);

            Assert.Contains(catalogue, x => x.Id == ownTemplate.Id);
            Assert.DoesNotContain(catalogue, x => x.Id == otherTemplate.Id);
        }

        [Fact]
        public async Task Allowed_user_can_be_archived_and_reactivated_case_insensitively()
        {
            var store = new FakeStore();
            var service = new HealthTrackerService(store, new FakeCurrentUser());

            var added = await service.AddAllowedUserAsync(
                new AddAllowedUserDto("Family@Example.com", "Member"),
                CancellationToken.None
            );
            await service.ArchiveAllowedUserAsync(added.Id, CancellationToken.None);
            var reactivated = await service.AddAllowedUserAsync(
                new AddAllowedUserDto("family@example.com", "Admin"),
                CancellationToken.None
            );

            Assert.Equal(added.Id, reactivated.Id);
            Assert.Equal("Admin", reactivated.Role);
            Assert.False(reactivated.IsArchived);
        }

        [Fact]
        public async Task Last_administrator_cannot_be_archived_or_demoted()
        {
            var store = new FakeStore();
            var service = new HealthTrackerService(store, new FakeCurrentUser());
            var administrator = Assert.Single(store.AllowedUsers);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ArchiveAllowedUserAsync(administrator.Id, CancellationToken.None)
            );
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UpdateAllowedUserRoleAsync(
                    administrator.Id,
                    new UpdateAllowedUserRoleDto("Member"),
                    CancellationToken.None
                )
            );
        }

        private sealed class FakeCurrentUser : ICurrentUser
        {
            public string Subject => "test-user";
            public string DisplayName => "Test user";
            public string Email => "test@example.com";
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
            public List<AllowedUser> AllowedUsers { get; } =
            [
                new()
                {
                    Email = "test@example.com",
                    NormalizedEmail = "TEST@EXAMPLE.COM",
                    Role = AllowedUserRole.Admin,
                },
            ];
            public List<ApplicationUser> Users { get; } = [];

            public Task<ApplicationUser?> FindUserBySubjectAsync(
                string subject,
                CancellationToken ct
            )
            {
                return Task.FromResult<ApplicationUser?>(subject == User.Subject ? User : null);
            }

            public Task AddUserAsync(ApplicationUser user, CancellationToken ct)
            {
                Users.Add(user);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
                Guid userId,
                CancellationToken ct
            )
            {
                return Task.FromResult<IReadOnlyCollection<MeasurementTemplate>>([
                    .. Templates.Where(x => x.OwnerUserId is null || x.OwnerUserId == userId),
                ]);
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

            public Task<ApplicationUser?> FindUserByIdAsync(Guid userId, CancellationToken ct) => Task.FromResult(Users.SingleOrDefault(x => x.Id == userId));

            public Task<AllowedUser?> FindAllowedUserByEmailAsync(
                string normalizedEmail,
                bool includeDeleted,
                CancellationToken ct
            )
            {
                return Task.FromResult(
                    AllowedUsers.SingleOrDefault(x =>
                        x.NormalizedEmail == normalizedEmail
                        && (includeDeleted || x.DeletedUtc is null)
                    )
                );
            }

            public Task<IReadOnlyCollection<AllowedUser>> GetAllowedUsersAsync(
                bool includeDeleted,
                CancellationToken ct
            )
            {
                return Task.FromResult<IReadOnlyCollection<AllowedUser>>(
                    [.. AllowedUsers.Where(x => includeDeleted || x.DeletedUtc is null)]
                );
            }

            public Task<int> CountActiveAdministratorsAsync(CancellationToken ct)
            {
                return Task.FromResult(
                    AllowedUsers.Count(x =>
                        x.Role == AllowedUserRole.Admin && x.DeletedUtc is null
                    )
                );
            }

            public Task AddAllowedUserAsync(AllowedUser user, CancellationToken ct)
            {
                AllowedUsers.Add(user);
                return Task.CompletedTask;
            }

            public Task UpdateAllowedUserAsync(AllowedUser user, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public Task<int> CountActiveTokensAsync(Guid allowedUserId, CancellationToken ct) => Task.FromResult(0);
            public Task<PersonalAccessToken?> FindActiveTokenByHashAsync(string hash, CancellationToken ct) => Task.FromResult<PersonalAccessToken?>(null);
            public Task<IReadOnlyCollection<PersonalAccessToken>> GetTokensAsync(Guid allowedUserId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<PersonalAccessToken>>([]);
            public Task AddTokenAsync(PersonalAccessToken token, CancellationToken ct) => Task.CompletedTask;
            public Task UpdateTokenAsync(PersonalAccessToken token, CancellationToken ct) => Task.CompletedTask;
            public Task AddMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken ct) => Task.CompletedTask;
            public Task<int> CountMcpCallsSinceAsync(Guid tokenId, DateTimeOffset sinceUtc, CancellationToken ct) => Task.FromResult(0);
            public Task<int> PurgeMcpAuditLogsAsync(DateTimeOffset beforeUtc, CancellationToken ct) => Task.FromResult(0);

            public Task<(IReadOnlyCollection<HealthReading> Items, int TotalCount)> GetReadingsPageAsync(
                Guid userId,
                Guid? templateId,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                int page,
                int pageSize,
                CancellationToken ct
            )
            {
                var readings = Readings
                    .Where(x =>
                        x.UserId == userId
                        && x.DeletedUtc is null
                        && (!templateId.HasValue || x.TemplateId == templateId)
                        && (!fromUtc.HasValue || x.RecordedAtUtc >= fromUtc)
                        && (!toUtc.HasValue || x.RecordedAtUtc <= toUtc)
                    )
                    .OrderByDescending(x => x.RecordedAtUtc)
                    .ToArray();
                var skip = (long)(page - 1) * pageSize;
                IReadOnlyCollection<HealthReading> items = skip >= readings.Length
                    ? []
                    : [.. readings.Skip((int)skip).Take(pageSize)];
                return Task.FromResult((items, readings.Length));
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
