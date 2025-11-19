using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Repository interface for webhook event persistence.
/// </summary>
public interface IWebhookRepository
{
    /// <summary>
    /// Adds a new webhook event to the database.
    /// </summary>
    Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a webhook event by provider and provider event ID (for deduplication).
    /// </summary>
    Task<WebhookEvent?> GetByProviderEventIdAsync(Guid providerId, string providerEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing webhook event.
    /// </summary>
    Task UpdateAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes webhook events older than the specified date (for cleanup).
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets webhook events that are pending retry.
    /// </summary>
    Task<List<WebhookEvent>> GetPendingRetriesAsync(int limit, CancellationToken cancellationToken = default);
}
