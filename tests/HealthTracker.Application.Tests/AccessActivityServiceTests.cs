using AwesomeAssertions;
using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;

namespace HealthTracker.Application.Tests;

public sealed class AccessActivityServiceTests
{
    [Fact]
    public async Task Record_normalizes_source_metadata_and_page_includes_the_user()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new AccessActivityService(store, new TestCurrentUser());
        var userAgent = "Browser\r\n" + new string('a', 600);

        // Act
        await service.RecordAsync(
            new RecordAccessActivityDto(
                store.CurrentAllowedUser.Id,
                AccessActivityType.WebSignIn,
                AccessActivityOutcome.Success,
                null,
                "2001:0db8:0:0:0:0:0:1",
                userAgent
            ),
            CancellationToken.None
        );
        var page = await service.GetPageAsync(null, null, null, 1, 50, CancellationToken.None);

        // Assert
        page.TotalCount.Should().Be(1);
        var activity = page.Items.Single();
        activity.UserEmail.Should().Be("test@example.com");
        activity.SourceIpAddress.Should().Be("2001:db8::1");
        activity.UserAgent.Should().NotContain("\r");
        activity.UserAgent.Should().NotContain("\n");
        activity.UserAgent.Should().HaveLength(512);
    }

    [Fact]
    public async Task Page_filters_newest_first_and_rejects_members()
    {
        // Arrange
        var store = new TestDataStore();
        store.AllowedUsers.Add(
            new AllowedUser
            {
                Email = "member@example.com",
                NormalizedEmail = "MEMBER@EXAMPLE.COM",
                Role = AllowedUserRole.Member,
            }
        );
        store.AccessActivities.AddRange(
            [
                new AccessActivity
                {
                    AllowedUserId = store.CurrentAllowedUser.Id,
                    Type = AccessActivityType.WebSignIn,
                    Outcome = AccessActivityOutcome.Success,
                    OccurredUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                },
                new AccessActivity
                {
                    Type = AccessActivityType.WebSignIn,
                    Outcome = AccessActivityOutcome.Failure,
                    FailureReason = AccessActivityFailureReason.ProviderFailure,
                    OccurredUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                },
                new AccessActivity
                {
                    AllowedUserId = store.CurrentAllowedUser.Id,
                    Type = AccessActivityType.AndroidAuthorization,
                    Outcome = AccessActivityOutcome.Success,
                    OccurredUtc = DateTimeOffset.UtcNow,
                },
            ]
        );
        var service = new AccessActivityService(store, new TestCurrentUser());

        // Act
        var page = await service.GetPageAsync(
            null,
            AccessActivityType.WebSignIn,
            null,
            1,
            50,
            CancellationToken.None
        );

        // Assert
        page.Items.Select(item => item.Outcome).Should().Equal("Failure", "Success");
        var memberService = new AccessActivityService(
            store,
            new TestCurrentUser(email: "member@example.com")
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => memberService.GetPageAsync(null, null, null, 1, 50, CancellationToken.None)
        );
    }
}
