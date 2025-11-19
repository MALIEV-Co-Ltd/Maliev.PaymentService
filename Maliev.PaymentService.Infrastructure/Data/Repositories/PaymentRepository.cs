using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for PaymentTransaction entity.
/// Provides data access using EF Core with optimistic concurrency control.
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a payment transaction by ID with provider and logs included.
    /// </summary>
    public async Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentTransactions
            .Include(p => p.PaymentProvider)
            .Include(p => p.TransactionLogs)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets a payment transaction by idempotency key.
    /// </summary>
    public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentTransactions
            .Include(p => p.PaymentProvider)
            .Include(p => p.TransactionLogs)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    /// <summary>
    /// Gets payment transactions within a date range, ordered by creation date descending.
    /// </summary>
    public async Task<IEnumerable<PaymentTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentTransactions
            .Include(p => p.PaymentProvider)
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new payment transaction.
    /// </summary>
    public async Task<PaymentTransaction> AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.PaymentTransactions.AddAsync(transaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <summary>
    /// Updates a payment transaction with optimistic concurrency check.
    /// Throws DbUpdateConcurrencyException if row version mismatch occurs.
    /// </summary>
    public async Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.PaymentTransactions.Update(transaction);
        await _context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    /// <summary>
    /// Adds a transaction log entry (immutable audit trail).
    /// </summary>
    public async Task AddLogAsync(TransactionLog log, CancellationToken cancellationToken = default)
    {
        await _context.TransactionLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
