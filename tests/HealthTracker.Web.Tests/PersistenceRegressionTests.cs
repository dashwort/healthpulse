using AwesomeAssertions;
using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence;
using HealthTracker.Infrastructure.Persistence.Models;

using Microsoft.EntityFrameworkCore;

namespace HealthTracker.Web.Tests;

public sealed class PersistenceRegressionTests
    : IClassFixture<HealthTrackerWebApplicationFactory>, IAsyncLifetime
{
    private readonly HealthTrackerWebApplicationFactory factory;

    public PersistenceRegressionTests(HealthTrackerWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Purge_soft_deleted_records_removes_expired_custom_history()
    {
        // Arrange
        var userId = await factory.SeedDevelopmentUserAsync();
        var template = new TemplateRecord
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            Name = "Expired metric",
            Category = "Custom",
            UnitCategory = "None",
            NormalizedUnit = "score",
            AllowedUnits = "score",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-90),
            DeletedUtc = DateTimeOffset.UtcNow.AddDays(-61),
        };
        var tracking = new TrackedTemplateRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TemplateId = template.Id,
            Template = template,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-90),
            DeletedUtc = DateTimeOffset.UtcNow.AddDays(-61),
        };
        var reading = new ReadingRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TemplateId = template.Id,
            Template = template,
            Value = 5,
            Unit = "score",
            RecordedAtUtc = DateTimeOffset.UtcNow.AddDays(-90),
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-90),
            DeletedUtc = DateTimeOffset.UtcNow.AddDays(-61),
        };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
            db.Templates.Add(template);
            db.TrackedTemplates.Add(tracking);
            db.Readings.Add(reading);
            await db.SaveChangesAsync();
        }
        using var storeScope = factory.Services.CreateScope();
        var store = storeScope.ServiceProvider.GetRequiredService<IHealthDataStore>();

        // Act
        var removed = await store.PurgeSoftDeletedAsync(
            DateTimeOffset.UtcNow.AddDays(-60),
            CancellationToken.None
        );

        // Assert
        removed.Should().Be(3);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        (await verifyDb.Templates.AnyAsync(item => item.Id == template.Id)).Should().BeFalse();
        (await verifyDb.TrackedTemplates.AnyAsync(item => item.Id == tracking.Id)).Should().BeFalse();
        (await verifyDb.Readings.AnyAsync(item => item.Id == reading.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Purge_mcp_audit_logs_removes_only_entries_before_the_cutoff()
    {
        // Arrange
        var oldLog = new McpAuditLogRecord
        {
            Id = Guid.NewGuid(),
            AllowedUserId = Guid.NewGuid(),
            PersonalAccessTokenId = Guid.NewGuid(),
            Method = "old",
            Outcome = "success",
            OccurredUtc = DateTimeOffset.UtcNow.AddYears(-2),
        };
        var recentLog = new McpAuditLogRecord
        {
            Id = Guid.NewGuid(),
            AllowedUserId = Guid.NewGuid(),
            PersonalAccessTokenId = Guid.NewGuid(),
            Method = "recent",
            Outcome = "success",
            OccurredUtc = DateTimeOffset.UtcNow,
        };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
            db.McpAuditLogs.AddRange(oldLog, recentLog);
            await db.SaveChangesAsync();
        }
        using var storeScope = factory.Services.CreateScope();
        var store = storeScope.ServiceProvider.GetRequiredService<IHealthDataStore>();

        // Act
        var removed = await store.PurgeMcpAuditLogsAsync(
            DateTimeOffset.UtcNow.AddYears(-1),
            CancellationToken.None
        );

        // Assert
        removed.Should().Be(1);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        (await verifyDb.McpAuditLogs.AnyAsync(item => item.Id == oldLog.Id)).Should().BeFalse();
        (await verifyDb.McpAuditLogs.AnyAsync(item => item.Id == recentLog.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Purge_access_activities_removes_only_entries_before_the_seven_day_cutoff()
    {
        // Arrange
        var oldActivity = new AccessActivityRecord
        {
            Id = Guid.NewGuid(),
            Type = "WebSignIn",
            Outcome = "Failure",
            FailureReason = "ProviderFailure",
            OccurredUtc = DateTimeOffset.UtcNow.AddDays(-8),
            SourceIpAddress = "192.0.2.1",
        };
        var recentActivity = new AccessActivityRecord
        {
            Id = Guid.NewGuid(),
            Type = "AndroidAuthorization",
            Outcome = "Success",
            OccurredUtc = DateTimeOffset.UtcNow.AddDays(-6),
            SourceIpAddress = "192.0.2.2",
        };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
            db.AccessActivities.AddRange(oldActivity, recentActivity);
            await db.SaveChangesAsync();
        }
        using var storeScope = factory.Services.CreateScope();
        var store = storeScope.ServiceProvider.GetRequiredService<IHealthDataStore>();

        // Act
        var removed = await store.PurgeAccessActivitiesAsync(
            DateTimeOffset.UtcNow.AddDays(-7),
            CancellationToken.None
        );

        // Assert
        removed.Should().Be(1);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        (await verifyDb.AccessActivities.AnyAsync(item => item.Id == oldActivity.Id)).Should().BeFalse();
        (await verifyDb.AccessActivities.AnyAsync(item => item.Id == recentActivity.Id)).Should().BeTrue();
    }
}
