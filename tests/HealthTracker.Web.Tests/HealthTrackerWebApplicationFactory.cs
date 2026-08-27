using HealthTracker.Infrastructure.Persistence;
using HealthTracker.Infrastructure.Persistence.Models;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HealthTracker.Web.Tests;

public sealed class HealthTrackerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public HealthTrackerWebApplicationFactory()
    {
        DatabasePath = Path.Combine(
            Path.GetTempPath(),
            "HealthPulseTests",
            $"healthpulse-{Guid.NewGuid():N}.db"
        );
    }

    public string DatabasePath { get; }

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:HealthTracker"] = $"Data Source={DatabasePath}",
                        ["AccessControl:InitialAdministratorEmail"] =
                            "developer@healthpulse.local",
                        ["Authentication:Development:Email"] = "developer@healthpulse.local",
                        ["Mobile:Android:LatestVersion"] = "1.2.3",
                        ["Mobile:Android:ApkUrl"] = "https://example.test/healthpulse.apk",
                        ["Mobile:Android:ReleaseNotes"] = "Test release",
                    }
                )
        );
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var response = await Client.GetAsync("/.well-known/healthpulse-mobile");
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();

        db.MobileSessions.RemoveRange(db.MobileSessions);
        db.MobileAuthorizationRequests.RemoveRange(db.MobileAuthorizationRequests);
        db.AccessActivities.RemoveRange(db.AccessActivities);
        db.McpAuditLogs.RemoveRange(db.McpAuditLogs);
        db.PersonalAccessTokens.RemoveRange(db.PersonalAccessTokens);
        db.Readings.RemoveRange(db.Readings);
        db.TrackedTemplates.RemoveRange(db.TrackedTemplates);
        db.Templates.RemoveRange(db.Templates.Where(template => template.OwnerUserId != null));
        db.AllowedUsers.RemoveRange(db.AllowedUsers);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();

        db.AllowedUsers.Add(
            new AllowedUserRecord
            {
                Id = Guid.NewGuid(),
                Email = "developer@healthpulse.local",
                NormalizedEmail = "DEVELOPER@HEALTHPULSE.LOCAL",
                Role = "Admin",
                CreatedUtc = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeTestAsync();

    private Task DisposeTestAsync()
    {
        Client.Dispose();
        Dispose();
        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }

        return Task.CompletedTask;
    }

    public async Task<Guid> SeedDevelopmentUserAsync(bool trackGlucose = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        var allowedUser = await db.AllowedUsers.SingleAsync(
            user => user.NormalizedEmail == "DEVELOPER@HEALTHPULSE.LOCAL"
        );
        var applicationUser = await db.Users.SingleOrDefaultAsync(
            user => user.Subject == "development-user"
        );
        if (applicationUser is null)
        {
            applicationUser = new UserRecord
            {
                Id = Guid.NewGuid(),
                Subject = "development-user",
                DisplayName = "Development user",
                CreatedUtc = DateTimeOffset.UtcNow,
            };
            db.Users.Add(applicationUser);
        }

        allowedUser.ApplicationUserId = applicationUser.Id;
        await db.SaveChangesAsync();

        if (trackGlucose)
        {
            var glucose = await db.Templates.SingleAsync(template => template.Code == "glucose");
            var alreadyTracked = await db.TrackedTemplates.AnyAsync(
                tracking =>
                    tracking.UserId == applicationUser.Id
                    && tracking.TemplateId == glucose.Id
            );
            if (!alreadyTracked)
            {
                db.TrackedTemplates.Add(
                    new TrackedTemplateRecord
                    {
                        Id = Guid.NewGuid(),
                        UserId = applicationUser.Id,
                        TemplateId = glucose.Id,
                        Template = glucose,
                        CreatedUtc = DateTimeOffset.UtcNow,
                    }
                );
                await db.SaveChangesAsync();
            }
        }

        return applicationUser.Id;
    }

    public async Task SeedTrackingAsync(Guid templateId)
    {
        var userId = await SeedDevelopmentUserAsync(trackGlucose: false);
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        var alreadyTracked = await db.TrackedTemplates.AnyAsync(
            tracking => tracking.UserId == userId && tracking.TemplateId == templateId
        );
        if (alreadyTracked)
        {
            return;
        }

        var template = await db.Templates.SingleAsync(item => item.Id == templateId);
        db.TrackedTemplates.Add(
            new TrackedTemplateRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TemplateId = templateId,
                Template = template,
                CreatedUtc = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();
    }

    public async Task<Guid> SeedReadingAsync(
        decimal value = 5.2m,
        string unit = "mmol/L",
        DateTimeOffset? recordedAtUtc = null
    )
    {
        var userId = await SeedDevelopmentUserAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HealthTrackerDbContext>();
        var template = await db.Templates.SingleAsync(item => item.Code == "glucose");
        var reading = new ReadingRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TemplateId = template.Id,
            Template = template,
            Value = value,
            Unit = unit,
            RecordedAtUtc = recordedAtUtc ?? DateTimeOffset.UtcNow.AddDays(-1),
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        db.Readings.Add(reading);
        await db.SaveChangesAsync();
        return reading.Id;
    }
}
