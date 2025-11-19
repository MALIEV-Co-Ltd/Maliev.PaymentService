using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for webhook event persistence.
/// </summary>
public class WebhookRepository : IWebhookRepository
{
    private readonly PaymentDbContext _context;

    public WebhookRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        _context.WebhookEvents.Add(webhookEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return webhookEvent;
    }

    public async Task<WebhookEvent?> GetByProviderEventIdAsync(Guid providerId, string providerEventId, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookEvents
            .FirstOrDefaultAsync(
                w => w.ProviderId == providerId && w.ProviderEventId == providerEventId,
                cancellationToken);
    }

    public async Task UpdateAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        webhookEvent.UpdatedAt = DateTime.UtcNow;
        _context.WebhookEvents.Update(webhookEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookEvents
            .Where(w => w.CreatedAt < date)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<List<WebhookEvent>> GetPendingRetriesAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookEvents
            .Where(w => w.ProcessingStatus == WebhookProcessingStatus.Failed &&
                       w.NextRetryAt != null &&
                       w.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(w => w.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
