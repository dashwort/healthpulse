using HealthTracker.Application.Abstractions;

namespace HealthTracker.Web.Services
{

    /// <summary>Persists in-memory request histograms in batches rather than writing one row per request.</summary>
    public sealed class TrafficMetricsFlushService(
        TrafficMetricBuffer buffer,
        IServiceScopeFactory scopeFactory,
        ILogger<TrafficMetricsFlushService> logger
    ) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await FlushAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Could not persist the request traffic summary batch.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The final batch is flushed from StopAsync while the host is shutting down.
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await FlushAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not persist the final request traffic summary batch.");
            }

            await base.StopAsync(cancellationToken);
        }

        private async Task FlushAsync(CancellationToken ct)
        {
            var metrics = buffer.Drain();
            if (metrics.Count == 0)
            {
                return;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IHealthDataStore>();
                await store.UpsertTrafficMetricBucketsAsync(metrics, ct);
            }
            catch
            {
                buffer.Restore(metrics);
                throw;
            }
        }
    }
}
