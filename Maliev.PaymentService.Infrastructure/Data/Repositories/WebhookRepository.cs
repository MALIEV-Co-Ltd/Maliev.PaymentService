using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        try
        {
            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync(cancellationToken);
            return webhookEvent;
        }
        catch (DbUpdateException ex) when (IsProviderEventUniqueViolation(ex))
        {
            _context.Entry(webhookEvent).State = EntityState.Detached;

            var existing = await GetByProviderEventIdAsync(
                webhookEvent.ProviderId,
                webhookEvent.ProviderEventId,
                cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            throw;
        }
    }

    public async Task<WebhookEvent?> GetByProviderEventIdAsync(Guid providerId, string providerEventId, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.ProviderId == providerId && w.ProviderEventId == providerEventId,
                cancellationToken);
    }

    public async Task<WebhookEvent?> GetByIdAsync(Guid webhookEventId, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == webhookEventId, cancellationToken);
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
            .AsNoTracking()
            .Where(w => w.ProcessingStatus == WebhookProcessingStatus.Failed &&
                       w.NextRetryAt != null &&
                       w.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(w => w.NextRetryAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static bool IsProviderEventUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
               string.Equals(
                   postgresException.ConstraintName,
                   "uk_webhook_events_provider_event",
                   StringComparison.Ordinal);
    }
}
