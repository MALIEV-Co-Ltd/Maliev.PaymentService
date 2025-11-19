using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Repository interface for PaymentTransaction entity operations.
/// Provides data access methods for payment processing with optimistic concurrency support.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Gets a payment transaction by its unique identifier.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment transaction if found, null otherwise</returns>
    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment transaction by its idempotency key.
    /// Used for duplicate detection and idempotent request handling.
    /// </summary>
    /// <param name="idempotencyKey">Idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment transaction if found, null otherwise</returns>
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payment transactions within a date range.
    /// Used for reporting and reconciliation.
    /// </summary>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of payment transactions in the date range</returns>
    Task<IEnumerable<PaymentTransaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new payment transaction.
    /// </summary>
    /// <param name="transaction">Payment transaction to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Added payment transaction</returns>
    Task<PaymentTransaction> AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing payment transaction with optimistic concurrency check.
    /// </summary>
    /// <param name="transaction">Payment transaction with updated data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated payment transaction</returns>
    /// <exception cref="DbUpdateConcurrencyException">Thrown when row version mismatch occurs</exception>
    Task<PaymentTransaction> UpdateAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a transaction log entry for audit trail.
    /// </summary>
    /// <param name="log">Transaction log to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddLogAsync(TransactionLog log, CancellationToken cancellationToken = default);
}
