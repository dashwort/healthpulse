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
        var page = await service.GetPageAsync(null, null, null, 1, 50, "24h", null, CancellationToken.None);

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
            "24h",
            null,
            CancellationToken.None
        );

        // Assert
        page.Items.Select(item => item.Outcome).Should().Equal("Failure", "Success");
        var memberService = new AccessActivityService(
            store,
            new TestCurrentUser(email: "member@example.com")
        );
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => memberService.GetPageAsync(null, null, null, 1, 50, "24h", null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Summary_counts_activity_and_flags_a_failure_burst_followed_by_success()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var store = new TestDataStore();
        for (var index = 0; index < 5; index++)
        {
            store.AccessActivities.Add(new AccessActivity
            {
                Type = AccessActivityType.WebSignIn,
                Outcome = AccessActivityOutcome.Failure,
                FailureReason = AccessActivityFailureReason.NotAllowed,
                SourceIpAddress = "203.0.113.10",
                OccurredUtc = now.AddMinutes(-10 + index),
            });
        }

        store.AccessActivities.Add(new AccessActivity
        {
            AllowedUserId = store.CurrentAllowedUser.Id,
            Type = AccessActivityType.AndroidAuthorization,
            Outcome = AccessActivityOutcome.Success,
            SourceIpAddress = "203.0.113.10",
            OccurredUtc = now.AddMinutes(-1),
        });
        store.AccessActivities.Add(new AccessActivity
        {
            AllowedUserId = store.CurrentAllowedUser.Id,
            Type = AccessActivityType.WebSignIn,
            Outcome = AccessActivityOutcome.Success,
            SourceIpAddress = "203.0.113.10",
            OccurredUtc = now.AddSeconds(-30),
        });
        store.AccessActivities.Add(new AccessActivity
        {
            Type = AccessActivityType.WebSignIn,
            Outcome = AccessActivityOutcome.Failure,
            FailureReason = AccessActivityFailureReason.ProviderFailure,
            SourceIpAddress = "203.0.113.20",
            OccurredUtc = now.AddMinutes(-30),
        });
        store.AccessActivities.Add(new AccessActivity
        {
            Type = AccessActivityType.WebSignIn,
            Outcome = AccessActivityOutcome.Failure,
            FailureReason = AccessActivityFailureReason.NotAllowed,
            SourceIpAddress = "203.0.113.10",
            OccurredUtc = now.AddDays(-8),
        });
        var service = new AccessActivityService(store, new TestCurrentUser());

        // Act
        var summary = await service.GetSummaryAsync("24h", CancellationToken.None);

        // Assert
        summary.Range.Should().Be("24h");
        summary.TotalEvents.Should().Be(8);
        summary.FailedWebSignIns.Should().Be(6);
        summary.SuccessfulAndroidAuthorizations.Should().Be(1);
        summary.SuccessfulWebSignIns.Should().Be(1);
        summary.DistinctSourceIpCount.Should().Be(2);
        summary.DistinctAccountCount.Should().Be(1);
        summary.DistinctSuccessfulAccountCount.Should().Be(1);
        summary.Timeline.Should().HaveCount(24);
        var cue = summary.ReviewCues.Should().ContainSingle().Subject;
        cue.SourceIpAddress.Should().Be("203.0.113.10");
        cue.FailureCount.Should().Be(5);
        cue.SuccessfulSignInAfterBurst.Should().BeTrue();
        cue.SuccessUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Android_approval_after_failure_burst_does_not_count_as_successful_sign_in()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var store = new TestDataStore();
        for (var index = 0; index < 5; index++)
        {
            store.AccessActivities.Add(new AccessActivity
            {
                Type = AccessActivityType.WebSignIn,
                Outcome = AccessActivityOutcome.Failure,
                FailureReason = AccessActivityFailureReason.NotAllowed,
                SourceIpAddress = "203.0.113.30",
                OccurredUtc = now.AddMinutes(-10 + index),
            });
        }

        store.AccessActivities.Add(new AccessActivity
        {
            AllowedUserId = store.CurrentAllowedUser.Id,
            Type = AccessActivityType.AndroidAuthorization,
            Outcome = AccessActivityOutcome.Success,
            SourceIpAddress = "203.0.113.30",
            OccurredUtc = now.AddMinutes(-1),
        });
        var service = new AccessActivityService(store, new TestCurrentUser());

        // Act
        var summary = await service.GetSummaryAsync("24h", CancellationToken.None);

        // Assert
        summary.ReviewCues.Should().ContainSingle().Which.SuccessfulSignInAfterBurst.Should().BeFalse();
        summary.DistinctSuccessfulAccountCount.Should().Be(0);
    }

    [Fact]
    public async Task Page_applies_selected_time_range_and_canonical_ip_filter()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var store = new TestDataStore();
        store.AccessActivities.AddRange(
            [
                new AccessActivity
                {
                    Type = AccessActivityType.WebSignIn,
                    Outcome = AccessActivityOutcome.Failure,
                    FailureReason = AccessActivityFailureReason.NotAllowed,
                    SourceIpAddress = "2001:db8::1",
                    OccurredUtc = now.AddMinutes(-1),
                },
                new AccessActivity
                {
                    Type = AccessActivityType.WebSignIn,
                    Outcome = AccessActivityOutcome.Failure,
                    FailureReason = AccessActivityFailureReason.NotAllowed,
                    SourceIpAddress = "2001:db8::2",
                    OccurredUtc = now.AddMinutes(-1),
                },
                new AccessActivity
                {
                    Type = AccessActivityType.WebSignIn,
                    Outcome = AccessActivityOutcome.Failure,
                    FailureReason = AccessActivityFailureReason.NotAllowed,
                    SourceIpAddress = "2001:db8::1",
                    OccurredUtc = now.AddDays(-2),
                },
            ]
        );
        var service = new AccessActivityService(store, new TestCurrentUser());

        // Act
        var page = await service.GetPageAsync(
            null,
            null,
            null,
            1,
            50,
            "24h",
            "2001:0db8:0:0:0:0:0:1",
            CancellationToken.None
        );

        // Assert
        page.TotalCount.Should().Be(1);
        page.Items.Single().SourceIpAddress.Should().Be("2001:db8::1");
    }
}
