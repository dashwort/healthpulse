using System.Text.Json;

using AwesomeAssertions;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;
using HealthTracker.Web.Mcp;

namespace HealthTracker.Application.Tests;

public sealed class HealthPulseMcpToolsTests
{
    [Fact]
    public async Task Import_json_rejects_more_than_the_supported_template_count()
    {
        // Arrange
        var tools = CreateTools();
        var json = JsonSerializer.Serialize(
            new
            {
                templates = Enumerable
                    .Range(0, 101)
                    .Select(index => new
                    {
                        id = Guid.NewGuid(),
                        name = $"Template {index}",
                        category = "Custom",
                        normalizedUnit = "unit",
                        isCustom = true,
                    }),
                readings = Array.Empty<object>(),
            }
        );
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await tools.ImportJson(json, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("record limit");
    }

    [Fact]
    public async Task Import_json_allows_a_bounded_empty_export_document()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var result = await tools.ImportJson(
            "{\"templates\":[],\"readings\":[]}",
            CancellationToken.None
        );

        // Assert
        result.Should().Be("Imported 0 custom templates and 0 readings.");
    }

    [Fact]
    public async Task List_templates_returns_the_catalogue_through_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        store.Templates.Add(BuiltInTemplates.All.Single(item => item.Code == "glucose"));
        var tools = CreateTools(store);

        // Act
        var templates = await tools.ListTemplates(CancellationToken.None);

        // Assert
        templates.Should().ContainSingle(item => item.Code == "glucose");
    }

    [Fact]
    public async Task Create_custom_template_uses_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        var tools = CreateTools(store);

        // Act
        var template = await tools.CreateCustomTemplate(
            "Sleep quality",
            "Lifestyle",
            "score",
            CancellationToken.None
        );

        // Assert
        template.Name.Should().Be("Sleep quality");
        template.IsCustom.Should().BeTrue();
        store.Trackings.Should().ContainSingle(item => item.TemplateId == template.Id);
    }

    [Fact]
    public async Task Update_custom_template_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithCustomTemplate();
        var tools = CreateTools(store);
        var template = store.Templates.Single();

        // Act
        var updated = await tools.UpdateCustomTemplate(
            template.Id,
            "Sleep duration",
            "Lifestyle",
            "hours",
            CancellationToken.None
        );

        // Assert
        updated.Name.Should().Be("Sleep duration");
        updated.NormalizedUnit.Should().Be("hours");
    }

    [Fact]
    public async Task Delete_custom_template_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithCustomTemplate();
        var tools = CreateTools(store);
        var template = store.Templates.Single();

        // Act
        await tools.DeleteCustomTemplate(template.Id, CancellationToken.None);

        // Assert
        template.DeletedUtc.Should().NotBeNull();
        store.Trackings.Single().DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Track_template_uses_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        var template = BuiltInTemplates.All.Single(item => item.Code == "weight");
        store.Templates.Add(template);
        var tools = CreateTools(store);

        // Act
        await tools.TrackTemplate(template.Id, CancellationToken.None);

        // Assert
        store.Trackings.Should().ContainSingle(item => item.TemplateId == template.Id);
    }

    [Fact]
    public async Task Untrack_template_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("weight");
        var tools = CreateTools(store);
        var template = store.Templates.Single();

        // Act
        await tools.UntrackTemplate(template.Id, CancellationToken.None);

        // Assert
        store.Trackings.Single().DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Add_reading_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var tools = CreateTools(store);
        var template = store.Templates.Single();

        // Act
        var reading = await tools.AddReading(
            template.Id,
            100m,
            "mg/dL",
            new DateTimeOffset(2024, 1, 15, 8, 30, 0, TimeSpan.Zero),
            "Fasting",
            CancellationToken.None
        );

        // Assert
        reading.Value.Should().BeApproximately(5.55m, 0.01m);
        reading.Unit.Should().Be("mmol/L");
        store.Readings.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_reading_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var reading = AddReading(store, template);
        var tools = CreateTools(store);

        // Act
        var updated = await tools.UpdateReading(
            reading.Id,
            180.182m,
            "mg/dL",
            new DateTimeOffset(2024, 1, 16, 8, 30, 0, TimeSpan.Zero),
            "Updated",
            CancellationToken.None
        );

        // Assert
        updated.Value.Should().BeApproximately(10m, 0.001m);
        updated.Note.Should().Be("Updated");
    }

    [Fact]
    public async Task List_readings_uses_the_public_mcp_api_and_applies_filters()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var template = store.Templates.Single();
        var reading = AddReading(
            store,
            template,
            new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)
        );
        AddReading(store, template, new DateTimeOffset(2024, 2, 2, 0, 0, 0, TimeSpan.Zero));
        var tools = CreateTools(store);

        // Act
        var readings = await tools.ListReadings(
            template.Id,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None
        );

        // Assert
        readings.Should().ContainSingle().Subject.Id.Should().Be(reading.Id);
    }

    [Fact]
    public async Task Delete_reading_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        var reading = AddReading(store, store.Templates.Single());
        var tools = CreateTools(store);

        // Act
        await tools.DeleteReading(reading.Id, CancellationToken.None);

        // Assert
        reading.DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Export_json_uses_the_public_mcp_api()
    {
        // Arrange
        var store = CreateStoreWithTrackedBuiltIn("glucose");
        AddReading(store, store.Templates.Single());
        var tools = CreateTools(store);

        // Act
        var json = await tools.ExportJson(CancellationToken.None);

        // Assert
        json.Should().Contain("templates");
        json.Should().Contain("readings");
        json.Should().Contain("glucose");
    }

    [Fact]
    public async Task Import_json_creates_custom_templates_and_maps_their_readings()
    {
        // Arrange
        var store = new TestDataStore();
        var tools = CreateTools(store);
        var templateId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(
            new
            {
                templates = new[]
                {
                    new
                    {
                        id = templateId,
                        name = "Sleep quality",
                        category = "Lifestyle",
                        normalizedUnit = "score",
                        isCustom = true,
                    },
                },
                readings = new[]
                {
                    new
                    {
                        templateId,
                        value = 8.5m,
                        unit = "score",
                        recordedAtUtc = "2024-01-15T08:30:00Z",
                        note = "Good",
                    },
                },
            }
        );

        // Act
        var result = await tools.ImportJson(json, CancellationToken.None);

        // Assert
        result.Should().Be("Imported 1 custom templates and 1 readings.");
        store.Templates.Should().ContainSingle(item => item.Name == "Sleep quality");
        store.Readings.Should().ContainSingle(item => item.Value == 8.5m);
    }

    [Fact]
    public async Task List_users_uses_the_public_mcp_api()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var users = await tools.ListUsers(false, CancellationToken.None);

        // Assert
        users.Should().ContainSingle(item => item.Role == "Admin");
    }

    [Fact]
    public async Task Add_user_uses_the_public_mcp_api()
    {
        // Arrange
        var tools = CreateTools();

        // Act
        var user = await tools.AddUser("member@example.com", "Member", CancellationToken.None);

        // Assert
        user.Email.Should().Be("member@example.com");
        user.Role.Should().Be("Member");
    }

    [Fact]
    public async Task Update_user_role_uses_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        var member = new AllowedUser
        {
            Email = "member@example.com",
            NormalizedEmail = "MEMBER@EXAMPLE.COM",
            Role = AllowedUserRole.Member,
        };
        store.AllowedUsers.Add(member);
        var tools = CreateTools(store);

        // Act
        var updated = await tools.UpdateUserRole(
            member.Id,
            "Admin",
            CancellationToken.None
        );

        // Assert
        updated.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Archive_user_uses_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        var member = new AllowedUser
        {
            Email = "member@example.com",
            NormalizedEmail = "MEMBER@EXAMPLE.COM",
            Role = AllowedUserRole.Member,
        };
        store.AllowedUsers.Add(member);
        var tools = CreateTools(store);

        // Act
        await tools.ArchiveUser(member.Id, CancellationToken.None);

        // Assert
        member.DeletedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Revoke_user_token_uses_the_public_mcp_api()
    {
        // Arrange
        var store = new TestDataStore();
        var member = new AllowedUser
        {
            Email = "member@example.com",
            NormalizedEmail = "MEMBER@EXAMPLE.COM",
            Role = AllowedUserRole.Member,
        };
        var token = new PersonalAccessToken
        {
            AllowedUserId = member.Id,
            Hash = "hash",
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
        };
        store.AllowedUsers.Add(member);
        store.Tokens.Add(token);
        var tools = CreateTools(store);

        // Act
        await tools.RevokeUserToken(member.Id, token.Id, CancellationToken.None);

        // Assert
        token.RevokedUtc.Should().NotBeNull();
    }

    private static HealthPulseMcpTools CreateTools(TestDataStore? store = null)
    {
        store ??= new TestDataStore();
        var currentUser = new TestCurrentUser();
        return new HealthPulseMcpTools(
            new HealthTrackerService(store, currentUser),
            new PersonalAccessTokenService(store, currentUser)
        );
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

    private static TestDataStore CreateStoreWithCustomTemplate()
    {
        var store = new TestDataStore();
        var template = new MeasurementTemplate
        {
            Id = Guid.NewGuid(),
            OwnerUserId = store.CurrentUser.Id,
            Name = "Sleep quality",
            Category = "Lifestyle",
            NormalizedUnit = "score",
            AllowedUnits = ["score"],
        };
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

    private static HealthReading AddReading(
        TestDataStore store,
        MeasurementTemplate template,
        DateTimeOffset? recordedAtUtc = null
    )
    {
        var reading = new HealthReading
        {
            UserId = store.CurrentUser.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Value = 5.2m,
            Unit = template.NormalizedUnit,
            RecordedAtUtc = recordedAtUtc ?? DateTimeOffset.UtcNow.AddDays(-1),
        };
        store.Readings.Add(reading);
        return reading;
    }
}
