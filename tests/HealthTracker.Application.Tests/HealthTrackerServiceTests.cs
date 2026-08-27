using AwesomeAssertions;
using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;

namespace HealthTracker.Application.Tests;

public sealed class HealthTrackerServiceTests
{
    [Fact]
    public async Task Create_reading_normalizes_built_in_units_and_preserves_a_short_note()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var service = CreateService(store);
        var recordedAt = new DateTimeOffset(2024, 1, 15, 8, 30, 0, TimeSpan.FromHours(1));

        // Act
        var result = await service.CreateReadingAsync(
            new CreateReadingDto(
                store.Templates.Single().Id,
                100m,
                "mg/dL",
                recordedAt,
                "Fasting"
            ),
            CancellationToken.None
        );

        // Assert
        result.Value.Should().BeApproximately(5.55m, 0.01m);
        result.Unit.Should().Be("mmol/L");
        result.Note.Should().Be("Fasting");
        result.RecordedAtUtc.Should().Be(recordedAt.ToUniversalTime());
    }

    [Fact]
    public async Task Create_reading_allows_historical_timestamps()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var service = CreateService(store);
        var recordedAt = new DateTimeOffset(2024, 1, 15, 8, 30, 0, TimeSpan.FromHours(1));

        // Act
        var result = await service.CreateReadingAsync(
            new CreateReadingDto(
                store.Templates.Single().Id,
                5.2m,
                "mmol/L",
                recordedAt,
                null
            ),
            CancellationToken.None
        );

        // Assert
        result.RecordedAtUtc.Should().Be(recordedAt.ToUniversalTime());
    }

    [Fact]
    public async Task Create_reading_rejects_a_note_over_140_characters()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var service = CreateService(store);
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.CreateReadingAsync(
                new CreateReadingDto(
                    store.Templates.Single().Id,
                    5m,
                    "mmol/L",
                    DateTimeOffset.UtcNow,
                    new string('x', 141)
                ),
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("140");
    }

    [Fact]
    public async Task Create_reading_rejects_a_timestamp_more_than_five_minutes_in_the_future()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var service = CreateService(store);
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.CreateReadingAsync(
                new CreateReadingDto(
                    store.Templates.Single().Id,
                    5m,
                    "mmol/L",
                    DateTimeOffset.UtcNow.AddMinutes(6),
                    null
                ),
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task Create_custom_template_tracks_it_and_keeps_its_custom_unit()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);

        // Act
        var template = await service.CreateCustomTemplateAsync(
            new CreateCustomTemplateDto("Sleep quality", "Lifestyle", "score"),
            CancellationToken.None
        );

        // Assert
        template.IsCustom.Should().BeTrue();
        template.IsTracked.Should().BeTrue();
        template.NormalizedUnit.Should().Be("score");
        store.Trackings.Should().ContainSingle(item => item.TemplateId == template.Id);
    }

    [Fact]
    public async Task Create_custom_reading_preserves_the_custom_value_and_unit()
    {
        // Arrange
        var store = new TestDataStore();
        var template = CustomTemplate(store.CurrentUser.Id, "Sleep quality");
        store.Templates.Add(template);
        store.Trackings.Add(
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                Template = template,
            }
        );
        var service = CreateService(store);

        // Act
        var reading = await service.CreateReadingAsync(
            new CreateReadingDto(
                template.Id,
                8.5m,
                "score",
                DateTimeOffset.UtcNow.AddDays(-1),
                "Good"
            ),
            CancellationToken.None
        );

        // Assert
        reading.Value.Should().Be(8.5m);
        reading.Unit.Should().Be("score");
        reading.Note.Should().Be("Good");
    }

    [Fact]
    public async Task Update_custom_template_changes_the_public_template_contract()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);
        var created = await service.CreateCustomTemplateAsync(
            new CreateCustomTemplateDto("Sleep quality", "Lifestyle", "score"),
            CancellationToken.None
        );

        // Act
        var updated = await service.UpdateCustomTemplateAsync(
            created.Id,
            new UpdateCustomTemplateDto("Sleep duration", "Lifestyle", "hours"),
            CancellationToken.None
        );

        // Assert
        updated.Name.Should().Be("Sleep duration");
        updated.NormalizedUnit.Should().Be("hours");
        updated.IsTracked.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_custom_template_soft_deletes_the_template_and_tracking()
    {
        // Arrange
        var store = new TestDataStore();
        var template = CustomTemplate(store.CurrentUser.Id, "Sleep quality");
        store.Templates.Add(template);
        store.Trackings.Add(
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                Template = template,
            }
        );
        var service = CreateService(store);

        // Act
        await service.DeleteCustomTemplateAsync(template.Id, CancellationToken.None);

        // Assert
        template.DeletedUtc.Should().NotBeNull();
        store.Trackings.Single().DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_template_reactivates_a_soft_deleted_tracking_selection()
    {
        // Arrange
        var store = new TestDataStore();
        var template = BuiltInTemplates.All.Single(item => item.Code == "weight");
        store.Templates.Add(template);
        store.Trackings.Add(
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                Template = template,
                DeletedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            }
        );
        var service = CreateService(store);

        // Act
        await service.TrackTemplateAsync(template.Id, CancellationToken.None);

        // Assert
        store.Trackings.Single().DeletedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Update_reading_normalizes_the_new_unit_and_note()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var reading = new HealthReading
        {
            UserId = store.CurrentUser.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Value = 5m,
            Unit = template.NormalizedUnit,
            RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        store.Readings.Add(reading);
        var service = CreateService(store);

        // Act
        var updated = await service.UpdateReadingAsync(
            reading.Id,
            new UpdateReadingDto(180.182m, "mg/dL", DateTimeOffset.UtcNow.AddDays(-2), "Updated"),
            CancellationToken.None
        );

        // Assert
        updated.Value.Should().BeApproximately(10m, 0.001m);
        updated.Unit.Should().Be("mmol/L");
        updated.Note.Should().Be("Updated");
    }

    [Fact]
    public async Task Get_readings_applies_template_and_date_filters()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var inRange = new HealthReading
        {
            UserId = store.CurrentUser.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Value = 5m,
            Unit = template.NormalizedUnit,
            RecordedAtUtc = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };
        store.Readings.AddRange(
            inRange,
            new HealthReading
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                TemplateName = template.Name,
                Value = 6m,
                Unit = template.NormalizedUnit,
                RecordedAtUtc = new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero),
            }
        );
        var service = CreateService(store);

        // Act
        var readings = await service.GetReadingsAsync(
            template.Id,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None
        );

        // Assert
        var onlyReading = readings.Should().ContainSingle().Subject;
        onlyReading.Id.Should().Be(inRange.Id);
    }

    [Fact]
    public async Task Get_tracked_templates_returns_only_active_user_tracking()
    {
        // Arrange
        var store = new TestDataStore();
        var active = BuiltInTemplates.All.Single(item => item.Code == "weight");
        var deleted = BuiltInTemplates.All.Single(item => item.Code == "urate");
        store.Templates.AddRange(active, deleted);
        store.Trackings.AddRange(
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = active.Id,
                Template = active,
            },
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = deleted.Id,
                Template = deleted,
                DeletedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            }
        );
        var service = CreateService(store);

        // Act
        var templates = await service.GetTrackedTemplatesAsync(CancellationToken.None);

        // Assert
        var onlyTemplate = templates.Should().ContainSingle().Subject;
        onlyTemplate.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task Create_reading_rejects_values_outside_the_supported_range()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var service = CreateService(store);
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.CreateReadingAsync(
                new CreateReadingDto(
                    store.Templates.Single().Id,
                    -1m,
                    "mmol/L",
                    DateTimeOffset.UtcNow,
                    null
                ),
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task Reading_operations_cannot_access_another_users_reading()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var reading = new HealthReading
        {
            UserId = Guid.NewGuid(),
            TemplateId = template.Id,
            TemplateName = template.Name,
            Value = 5m,
            Unit = template.NormalizedUnit,
            RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        store.Readings.Add(reading);
        var service = CreateService(store);
        KeyNotFoundException? exception = null;

        // Act
        try
        {
            await service.DeleteReadingAsync(reading.Id, CancellationToken.None);
        }
        catch (KeyNotFoundException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        reading.DeletedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Delete_reading_soft_deletes_the_owned_reading()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var reading = new HealthReading
        {
            UserId = store.CurrentUser.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Value = 5m,
            Unit = template.NormalizedUnit,
            RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        };
        store.Readings.Add(reading);
        var service = CreateService(store);

        // Act
        await service.DeleteReadingAsync(reading.Id, CancellationToken.None);

        // Assert
        reading.DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Non_administrators_cannot_list_allowed_users()
    {
        // Arrange
        var store = new TestDataStore(role: AllowedUserRole.Member);
        var service = CreateService(store);
        UnauthorizedAccessException? exception = null;

        // Act
        try
        {
            await service.GetAllowedUsersAsync(false, CancellationToken.None);
        }
        catch (UnauthorizedAccessException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task Unknown_current_subject_cannot_read_health_data()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new HealthTrackerService(
            store,
            new TestCurrentUser(subject: "", email: "test@example.com")
        );
        UnauthorizedAccessException? exception = null;

        // Act
        try
        {
            await service.GetCatalogueAsync(CancellationToken.None);
        }
        catch (UnauthorizedAccessException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task Catalogue_excludes_another_users_custom_template()
    {
        // Arrange
        var store = new TestDataStore();
        var ownTemplate = CustomTemplate(store.CurrentUser.Id, "Own metric");
        var otherTemplate = CustomTemplate(Guid.NewGuid(), "Private metric");
        store.Templates.AddRange([ownTemplate, otherTemplate]);
        var service = CreateService(store);

        // Act
        var catalogue = await service.GetCatalogueAsync(CancellationToken.None);

        // Assert
        catalogue.Should().Contain(item => item.Id == ownTemplate.Id);
        catalogue.Should().NotContain(item => item.Id == otherTemplate.Id);
    }

    [Fact]
    public async Task Stop_tracking_soft_deletes_tracking_without_deleting_readings()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        store.Readings.Add(
            new HealthReading
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                TemplateName = template.Name,
                Value = 5.2m,
                Unit = template.NormalizedUnit,
                RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            }
        );
        var service = CreateService(store);

        // Act
        await service.StopTrackingAsync(template.Id, CancellationToken.None);

        // Assert
        store.Trackings.Single().DeletedUtc.Should().NotBeNull();
        store.Readings.Should().ContainSingle();
    }

    [Fact]
    public async Task Reading_page_filters_and_reports_total_count()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        store.Readings.AddRange(
            Enumerable
                .Range(1, 3)
                .Select(index => new HealthReading
                {
                    UserId = store.CurrentUser.Id,
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Value = index,
                    Unit = template.NormalizedUnit,
                    RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-index),
                })
        );
        var service = CreateService(store);

        // Act
        var page = await service.GetReadingPageAsync(
            template.Id,
            null,
            null,
            2,
            2,
            CancellationToken.None
        );

        // Assert
        page.TotalCount.Should().Be(3);
        page.Items.Should().ContainSingle();
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Metric_summaries_report_latest_value_and_change()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        store.Readings.AddRange(
            [
                new HealthReading
                {
                    UserId = store.CurrentUser.Id,
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Value = 6m,
                    Unit = template.NormalizedUnit,
                    RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                },
                new HealthReading
                {
                    UserId = store.CurrentUser.Id,
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Value = 5m,
                    Unit = template.NormalizedUnit,
                    RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                },
            ]
        );
        var service = CreateService(store);

        // Act
        var summaries = await service.GetMetricSummariesAsync(CancellationToken.None);

        // Assert
        var summary = summaries.Should().ContainSingle().Subject;
        summary.LatestValue.Should().Be(6m);
        summary.ChangeFromPrevious.Should().Be(1m);
    }

    [Fact]
    public async Task Allowed_user_can_be_archived_and_reactivated_case_insensitively()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);
        var added = await service.AddAllowedUserAsync(
            new AddAllowedUserDto("Family@Example.com", "Member"),
            CancellationToken.None
        );
        await service.ArchiveAllowedUserAsync(added.Id, CancellationToken.None);

        // Act
        var reactivated = await service.AddAllowedUserAsync(
            new AddAllowedUserDto("family@example.com", "Admin"),
            CancellationToken.None
        );

        // Assert
        reactivated.Id.Should().Be(added.Id);
        reactivated.Role.Should().Be("Admin");
        reactivated.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Archiving_a_user_revokes_tokens_that_reactivation_does_not_restore()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);
        var user = await service.AddAllowedUserAsync(
            new AddAllowedUserDto("Family@Example.com", "Member"),
            CancellationToken.None
        );
        var token = new PersonalAccessToken
        {
            AllowedUserId = user.Id,
            Hash = "test-hash",
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
        };
        store.Tokens.Add(token);

        // Act
        await service.ArchiveAllowedUserAsync(user.Id, CancellationToken.None);

        // Assert
        token.RevokedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Last_administrator_cannot_be_archived()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.ArchiveAllowedUserAsync(
                store.CurrentAllowedUser.Id,
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("administrator");
    }

    [Fact]
    public async Task Last_administrator_cannot_be_demoted()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.UpdateAllowedUserRoleAsync(
                store.CurrentAllowedUser.Id,
                new UpdateAllowedUserRoleDto("Member"),
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("administrator");
    }

    [Fact]
    public async Task Execute_in_transaction_delegates_to_the_data_store()
    {
        // Arrange
        var store = new TestDataStore();
        var service = CreateService(store);

        // Act
        var result = await service.ExecuteInTransactionAsync(
            () => Task.FromResult("committed"),
            CancellationToken.None
        );

        // Assert
        result.Should().Be("committed");
        store.TransactionCount.Should().Be(1);
    }

    private static TestDataStore CreateStoreWithTrackedBuiltIn(string code)
    {
        var store = new TestDataStore();
        var template = BuiltInTemplates.All.Single(item => item.Code == code);
        store.Templates.Add(template);
        store.Trackings.Add(
            new UserTrackedTemplate
            {
                UserId = store.CurrentUser.Id,
                TemplateId = template.Id,
                Template = template,
            }
        );
        return store;
    }

    private static HealthTrackerService CreateService(TestDataStore store) =>
        new(store, new TestCurrentUser());

    private static MeasurementTemplate CustomTemplate(Guid ownerUserId, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Name = name,
            Category = "Custom",
            NormalizedUnit = "unit",
            AllowedUnits = ["unit"],
        };
}
