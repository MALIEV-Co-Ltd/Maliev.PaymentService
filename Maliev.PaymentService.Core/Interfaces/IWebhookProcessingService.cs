using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service for processing webhook events from payment providers.
/// Handles deduplication, transaction updates, and event publishing.
/// </summary>
public interface IWebhookProcessingService
{
    /// <summary>
    /// Processes a webhook event.
    /// Includes deduplication, status updates, and event publishing.
    /// </summary>
    /// <param name="webhookEvent">Webhook event to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with status and any error messages</returns>
    Task<WebhookProcessingResult> ProcessWebhookAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries failed webhook processing.
    /// </summary>
    /// <param name="webhookEventId">ID of webhook event to retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<WebhookProcessingResult> RetryWebhookAsync(Guid webhookEventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of webhook processing operation.
/// </summary>
public class WebhookProcessingResult
{
    /// <summary>
    /// Whether processing was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Whether the webhook was a duplicate.
    /// </summary>
    public required bool IsDuplicate { get; init; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Updated transaction ID if applicable.
    /// </summary>
    public Guid? TransactionId { get; init; }
}
