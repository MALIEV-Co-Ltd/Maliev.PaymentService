using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for RefundTransaction entity.
/// Provides data access using EF Core and PostgreSQL.
/// </summary>
public class RefundRepository : IRefundRepository
{
    private readonly PaymentDbContext _context;

    public RefundRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<RefundTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RefundTransactions
            .Include(r => r.PaymentTransaction)
            .Include(r => r.Provider)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<List<RefundTransaction>> GetByPaymentTransactionIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RefundTransactions
            .Where(r => r.PaymentTransactionId == paymentTransactionId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RefundTransaction> AddAsync(RefundTransaction refund, CancellationToken cancellationToken = default)
    {
        await _context.RefundTransactions.AddAsync(refund, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return refund;
    }

    public async Task<RefundTransaction> UpdateAsync(RefundTransaction refund, CancellationToken cancellationToken = default)
    {
        refund.UpdatedAt = DateTime.UtcNow;
        _context.RefundTransactions.Update(refund);
        await _context.SaveChangesAsync(cancellationToken);
        return refund;
    }
}
