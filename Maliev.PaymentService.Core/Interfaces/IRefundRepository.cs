using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Repository interface for RefundTransaction entity operations.
/// </summary>
public interface IRefundRepository
{
    /// <summary>
    /// Gets a refund by its unique identifier.
    /// </summary>
    /// <param name="id">Refund ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refund if found, null otherwise</returns>
    Task<RefundTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all refunds for a specific payment transaction.
    /// </summary>
    /// <param name="paymentTransactionId">Payment transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of refunds (newest first)</returns>
    Task<List<RefundTransaction>> GetByPaymentTransactionIdAsync(Guid paymentTransactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new refund transaction.
    /// </summary>
    /// <param name="refund">Refund to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Added refund</returns>
    Task<RefundTransaction> AddAsync(RefundTransaction refund, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing refund transaction.
    /// </summary>
    /// <param name="refund">Refund with updated data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated refund</returns>
    Task<RefundTransaction> UpdateAsync(RefundTransaction refund, CancellationToken cancellationToken = default);
}
