using HealthTracker.Application.Abstractions;

namespace HealthTracker.Web.Services
{
    public sealed class SoftDeletionPurgeService(
        IServiceScopeFactory scopeFactory,
        ILogger<SoftDeletionPurgeService> logger
    ) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await PurgeAsync(stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PurgeAsync(stoppingToken);
            }
        }

        private async Task PurgeAsync(CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IHealthDataStore>();
            var deleted = await store.PurgeSoftDeletedAsync(DateTimeOffset.UtcNow.AddDays(-60), ct);
            await store.PurgeMcpAuditLogsAsync(DateTimeOffset.UtcNow.AddYears(-1), ct);
            await store.PurgeAccessActivitiesAsync(DateTimeOffset.UtcNow.AddDays(-7), ct);
            if (deleted > 0)
            {
                logger.LogInformation(
                    "Permanently removed {Count} records that had been soft-deleted for 60 days.",
                    deleted
                );
            }
        }
    }
}
