using Maliev.PaymentService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Background service for retrying failed webhook processing attempts.
/// </summary>
public class WebhookRetryService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookRetryService> _logger;

    public WebhookRetryService(
        IServiceProvider serviceProvider,
        ILogger<WebhookRetryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<int> ProcessDueRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var webhookRepository = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var processingService = scope.ServiceProvider.GetRequiredService<IWebhookProcessingService>();

        var dueWebhooks = await webhookRepository.GetPendingRetriesAsync(BatchSize, cancellationToken);
        if (dueWebhooks.Count == 0)
        {
            return 0;
        }

        var processedCount = 0;
        foreach (var webhook in dueWebhooks)
        {
            try
            {
                var result = await processingService.RetryWebhookAsync(webhook.Id, cancellationToken);
                if (result.Success)
                {
                    processedCount++;
                }
                else
                {
                    _logger.LogWarning(
                        "Webhook retry failed for {WebhookId}: {ErrorMessage}",
                        webhook.Id,
                        result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrying webhook {WebhookId}", webhook.Id);
            }
        }

        return processedCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookRetryService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessDueRetriesAsync(stoppingToken);
                if (processedCount > 0)
                {
                    _logger.LogInformation("Retried {ProcessedCount} failed webhook events", processedCount);
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebhookRetryService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebhookRetryService main loop");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("WebhookRetryService stopped");
    }
}
