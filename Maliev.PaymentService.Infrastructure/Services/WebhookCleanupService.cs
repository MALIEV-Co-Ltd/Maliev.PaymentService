using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Background service for cleaning up old webhook events.
/// Runs daily at 2 AM UTC to delete webhooks older than 30 days.
/// </summary>
public class WebhookCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookCleanupService> _logger;
    private const int RetentionDays = 30;

    public WebhookCleanupService(
        IServiceProvider serviceProvider,
        ILogger<WebhookCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next run time (2 AM UTC)
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddHours(2);
                if (now >= nextRun)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;
                _logger.LogInformation(
                    "Next webhook cleanup scheduled for {NextRun} UTC (in {DelayHours:F2} hours)",
                    nextRun, delay.TotalHours);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await CleanupOldWebhooksAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebhookCleanupService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebhookCleanupService main loop");
                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("WebhookCleanupService stopped");
    }

    private async Task CleanupOldWebhooksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting webhook cleanup for events older than {RetentionDays} days", RetentionDays);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

            // Count webhooks to delete
            var webhooksToDelete = await dbContext.WebhookEvents
                .Where(w => w.CreatedAt < cutoffDate)
                .CountAsync(cancellationToken);

            if (webhooksToDelete == 0)
            {
                _logger.LogInformation("No webhook events to clean up");
                return;
            }

            _logger.LogInformation("Found {Count} webhook events to delete", webhooksToDelete);

            // Delete in batches to avoid locking the table for too long
            const int batchSize = 1000;
            var totalDeleted = 0;

            while (true)
            {
                var batch = await dbContext.WebhookEvents
                    .Where(w => w.CreatedAt < cutoffDate)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                dbContext.WebhookEvents.RemoveRange(batch);
                await dbContext.SaveChangesAsync(cancellationToken);

                totalDeleted += batch.Count;
                _logger.LogInformation(
                    "Deleted batch of {BatchCount} webhooks. Total: {TotalDeleted}/{TotalToDelete}",
                    batch.Count, totalDeleted, webhooksToDelete);

                // Add a small delay between batches to reduce database load
                await Task.Delay(100, cancellationToken);
            }

            _logger.LogInformation(
                "Webhook cleanup completed. Deleted {TotalDeleted} webhook events older than {CutoffDate}",
                totalDeleted, cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during webhook cleanup");
            throw;
        }
    }
}
