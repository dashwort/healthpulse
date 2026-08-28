using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;
using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence;
using HealthTracker.Infrastructure.Persistence.Models;
using HealthTracker.Web.Services;

using Microsoft.EntityFrameworkCore;

namespace HealthTracker.Web.Tests;

public sealed class ApiRegressionTests
    : IClassFixture<HealthTrackerWebApplicationFactory>, IAsyncLifetime
{
    private readonly HealthTrackerWebApplicationFactory factory;

    public ApiRegressionTests(HealthTrackerWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Mobile_discovery_describes_the_public_endpoints()
    {
        // Arrange

        // Act
        var document = await factory.Client.GetFromJsonAsync<JsonElement>(
            "/.well-known/healthpulse-mobile"
        );

        // Assert
        document.GetProperty("product").GetString().Should().Be("HealthPulse");
        document.GetProperty("apiVersion").GetInt32().Should().Be(1);
        document.GetProperty("authorizationEndpoint").GetString()
            .Should().Be("/api/mobile/auth/authorize");
        document.GetProperty("tokenEndpoint").GetString().Should().Be("/api/mobile/auth/token");
    }

    [Fact]
    public async Task Android_update_endpoint_returns_the_configured_release()
    {
        // Arrange

        // Act
        var document = await factory.Client.GetFromJsonAsync<JsonElement>(
            "/.well-known/healthpulse-android-update"
        );

        // Assert
        document.GetProperty("latestVersion").GetString().Should().Be("1.2.3");
        document.GetProperty("apkUrl").GetString().Should().Be("https://example.test/healthpulse.apk");
        document.GetProperty("releaseNotes").GetString().Should().Be("Test release");
    }

    [Fact]
    public async Task Mobile_authorization_endpoint_returns_a_completion_redirect()
    {
        // Arrange
        var challenge = new string('a', 43);
        var query =
            $"?code_challenge={Uri.EscapeDataString(challenge)}&state=state-1&redirect_uri={Uri.EscapeDataString("healthpulse://auth/callback")}";

        // Act
        var response = await factory.Client.GetAsync("/api/mobile/auth/authorize" + query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/api/mobile/auth/complete");
        response.Headers.Location.Query.Should().Contain("requestId=");
    }

    [Fact]
    public async Task Mobile_authorization_endpoint_rejects_an_unsupported_redirect_uri()
    {
        // Arrange
        var challenge = new string('a', 43);
        var query =
            $"?code_challenge={Uri.EscapeDataString(challenge)}&state=state-1&redirect_uri={Uri.EscapeDataString("https://example.test/callback")}";

        // Act
        var response = await factory.Client.GetAsync("/api/mobile/auth/authorize" + query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mobile_token_endpoint_rejects_an_unknown_grant_type()
    {
        // Arrange

        // Act
        var response = await factory.Client.PostAsJsonAsync(
            "/api/mobile/auth/token",
            new { grantType = "unsupported" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mobile_bearer_authentication_allows_a_seeded_user_to_read()
    {
        // Arrange
        var userId = await factory.SeedDevelopmentUserAsync();
        var accessToken = "hpma_" + new string('a', 64);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
            db.MobileSessions.Add(
                new MobileSessionRecord
                {
                    Id = Guid.NewGuid(),
                    ApplicationUserId = userId,
                    AccessTokenHash = MobileAuthenticationService.Hash(accessToken),
                    AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
                    RefreshTokenHash = MobileAuthenticationService.Hash("refresh"),
                    RefreshTokenExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
                    CreatedUtc = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/readings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await factory.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Catalogue_returns_all_seeded_builtin_templates()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetAsync("/api/templates/catalogue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        templates.Should().NotBeNull();
        templates!.Count(item => item.GetProperty("code").GetString() is not null)
            .Should()
            .Be(BuiltInTemplates.All.Count);
        templates.Select(item => item.GetProperty("code").GetString())
            .Should()
            .Contain("glucose");
    }

    [Fact]
    public async Task Tracked_endpoint_returns_only_the_current_users_tracking()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetAsync("/api/templates/tracked");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        templates.Should().ContainSingle(item => item.GetProperty("code").GetString() == "glucose");
    }

    [Fact]
    public async Task Custom_template_endpoint_creates_and_tracks_a_template()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var request = new { name = "Sleep quality", category = "Lifestyle", unit = "score" };

        // Act
        var response = await factory.Client.PostAsJsonAsync("/api/templates/custom", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var template = await response.Content.ReadFromJsonAsync<JsonElement>();
        template.GetProperty("name").GetString().Should().Be("Sleep quality");
        template.GetProperty("isCustom").GetBoolean().Should().BeTrue();
        template.GetProperty("isTracked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Custom_template_endpoint_updates_an_owned_template()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var created = await factory.Client.PostAsJsonAsync(
            "/api/templates/custom",
            new { name = "Sleep quality", category = "Lifestyle", unit = "score" }
        );
        var createdTemplate = await created.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createdTemplate.GetProperty("id").GetGuid();

        // Act
        var response = await factory.Client.PutAsJsonAsync(
            $"/api/templates/custom/{templateId}",
            new { name = "Sleep duration", category = "Lifestyle", unit = "hours" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var template = await response.Content.ReadFromJsonAsync<JsonElement>();
        template.GetProperty("name").GetString().Should().Be("Sleep duration");
        template.GetProperty("normalizedUnit").GetString().Should().Be("hours");
    }

    [Fact]
    public async Task Custom_template_endpoint_soft_deletes_an_owned_template()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var created = await factory.Client.PostAsJsonAsync(
            "/api/templates/custom",
            new { name = "Sleep quality", category = "Lifestyle", unit = "score" }
        );
        var createdTemplate = await created.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createdTemplate.GetProperty("id").GetGuid();

        // Act
        var response = await factory.Client.DeleteAsync($"/api/templates/custom/{templateId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var catalogue = await factory.Client.GetFromJsonAsync<JsonElement[]>("/api/templates/catalogue");
        catalogue.Should().NotContain(item => item.GetProperty("id").GetGuid() == templateId);
    }

    [Fact]
    public async Task Custom_reading_endpoint_preserves_the_custom_unit()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var created = await factory.Client.PostAsJsonAsync(
            "/api/templates/custom",
            new { name = "Sleep quality", category = "Lifestyle", unit = "score" }
        );
        var createdTemplate = await created.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createdTemplate.GetProperty("id").GetGuid();
        var request = new
        {
            templateId,
            value = 8.5m,
            unit = "score",
            recordedAtUtc = "2024-01-15T07:30:00Z",
            note = "Good",
        };

        // Act
        var response = await factory.Client.PostAsJsonAsync("/api/readings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        reading.GetProperty("value").GetDecimal().Should().Be(8.5m);
        reading.GetProperty("unit").GetString().Should().Be("score");
    }

    [Fact]
    public async Task Reading_endpoint_creates_a_normalized_historical_reading()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var glucoseId = BuiltInTemplates.All.Single(item => item.Code == "glucose").Id;
        var request = new
        {
            templateId = glucoseId,
            value = 100m,
            unit = "mg/dL",
            recordedAtUtc = "2024-01-15T07:30:00Z",
            note = "Fasting",
        };

        // Act
        var response = await factory.Client.PostAsJsonAsync("/api/readings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        reading.GetProperty("unit").GetString().Should().Be("mmol/L");
        reading.GetProperty("note").GetString().Should().Be("Fasting");
        reading.GetProperty("recordedAtUtc").GetDateTimeOffset().Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Reading_endpoint_rejects_invalid_page_size()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetAsync("/api/readings?pageSize=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("invalid");
    }

    [Fact]
    public async Task Reading_endpoint_updates_an_existing_reading()
    {
        // Arrange
        var readingId = await factory.SeedReadingAsync();
        var request = new
        {
            value = 180.182m,
            unit = "mg/dL",
            recordedAtUtc = "2024-01-16T08:30:00Z",
            note = "Updated",
        };

        // Act
        var response = await factory.Client.PutAsJsonAsync($"/api/readings/{readingId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        reading.GetProperty("value").GetDecimal().Should().BeApproximately(10m, 0.001m);
        reading.GetProperty("unit").GetString().Should().Be("mmol/L");
        reading.GetProperty("note").GetString().Should().Be("Updated");
    }

    [Fact]
    public async Task Reading_endpoint_soft_deletes_an_existing_reading()
    {
        // Arrange
        var readingId = await factory.SeedReadingAsync();

        // Act
        var response = await factory.Client.DeleteAsync($"/api/readings/{readingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var page = await factory.Client.GetFromJsonAsync<JsonElement>("/api/readings");
        page.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Template_tracking_endpoint_can_enable_another_builtin_template()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var weightId = BuiltInTemplates.All.Single(item => item.Code == "weight").Id;

        // Act
        var response = await factory.Client.PostAsync($"/api/templates/{weightId}/track", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var tracked = await factory.Client.GetFromJsonAsync<JsonElement[]>("/api/templates/tracked");
        tracked.Should().Contain(item => item.GetProperty("code").GetString() == "weight");
    }

    [Fact]
    public async Task Template_tracking_endpoint_can_stop_tracking_without_removing_history()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var glucoseId = BuiltInTemplates.All.Single(item => item.Code == "glucose").Id;

        // Act
        var response = await factory.Client.DeleteAsync($"/api/templates/{glucoseId}/track");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var catalogue = await factory.Client.GetFromJsonAsync<JsonElement[]>("/api/templates/catalogue");
        catalogue.Should().Contain(item => item.GetProperty("code").GetString() == "glucose");
        catalogue!.Single(item => item.GetProperty("code").GetString() == "glucose")
            .GetProperty("isTracked")
            .GetBoolean()
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task User_endpoint_lists_the_seeded_administrator()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        users.Should().ContainSingle(item => item.GetProperty("role").GetString() == "Admin");
    }

    [Fact]
    public async Task User_endpoint_adds_a_member_to_the_allow_list()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var email = $"member-{Guid.NewGuid():N}@example.test";

        // Act
        var response = await factory.Client.PostAsJsonAsync(
            "/api/users",
            new { email, role = "Member" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        user.GetProperty("email").GetString().Should().Be(email);
        user.GetProperty("role").GetString().Should().Be("Member");
    }

    [Fact]
    public async Task User_endpoint_updates_a_member_role()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var email = $"role-{Guid.NewGuid():N}@example.test";
        var created = await factory.Client.PostAsJsonAsync(
            "/api/users",
            new { email, role = "Member" }
        );
        var createdUser = await created.Content.ReadFromJsonAsync<JsonElement>();
        var userId = createdUser.GetProperty("id").GetGuid();

        // Act
        var response = await factory.Client.PutAsJsonAsync(
            $"/api/users/{userId}/role",
            new { role = "Admin" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        user.GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task User_endpoint_archives_a_member_and_can_include_it_in_the_archive_view()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();
        var email = $"archive-{Guid.NewGuid():N}@example.test";
        var created = await factory.Client.PostAsJsonAsync(
            "/api/users",
            new { email, role = "Member" }
        );
        var createdUser = await created.Content.ReadFromJsonAsync<JsonElement>();
        var userId = createdUser.GetProperty("id").GetGuid();

        // Act
        var response = await factory.Client.DeleteAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var users = await factory.Client.GetFromJsonAsync<JsonElement[]>("/api/users?includeArchived=true");
        users.Should().Contain(
            item =>
                item.GetProperty("id").GetGuid() == userId
                && item.GetProperty("isArchived").GetBoolean()
        );
    }

    [Fact]
    public async Task App_session_describes_the_authenticated_administrator()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetFromJsonAsync<JsonElement>("/api/app/session");

        // Assert
        response.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        response.GetProperty("isAdministrator").GetBoolean().Should().BeTrue();
        response.GetProperty("antiforgeryToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task App_info_returns_deployment_and_android_release_details()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var response = await factory.Client.GetFromJsonAsync<JsonElement>("/api/app/info");

        // Assert
        response.GetProperty("deployment").GetProperty("version").GetString()
            .Should().NotBeNullOrWhiteSpace();
        response.GetProperty("android").GetProperty("latestVersion").GetString()
            .Should().Be("1.2.3");
    }

    [Fact]
    public async Task Token_endpoints_create_list_and_revoke_a_personal_token()
    {
        // Arrange
        await factory.SeedDevelopmentUserAsync();

        // Act
        var createdResponse = await factory.Client.PostAsJsonAsync(
            "/api/tokens",
            new { name = "Automation" }
        );

        // Assert
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("secret").GetString().Should().StartWith("hp_");
        var tokenId = created.GetProperty("token").GetProperty("id").GetGuid();

        var listed = await factory.Client.GetFromJsonAsync<JsonElement[]>("/api/tokens");
        listed.Should().ContainSingle(item => item.GetProperty("id").GetGuid() == tokenId);

        var revoked = await factory.Client.DeleteAsync($"/api/tokens/{tokenId}");
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
