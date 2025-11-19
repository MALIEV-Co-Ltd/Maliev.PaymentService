using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service interface for refund processing operations.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Processes a refund for a completed payment.
    /// Validates refund amount, checks remaining refundable amount, calls provider adapter.
    /// </summary>
    /// <param name="paymentTransactionId">ID of the payment to refund</param>
    /// <param name="amount">Refund amount (must be > 0 and <= remaining refundable)</param>
    /// <param name="reason">Reason for refund (customer-facing)</param>
    /// <param name="refundType">"full" or "partial"</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created refund transaction</returns>
    Task<RefundTransaction> ProcessRefundAsync(
        Guid paymentTransactionId,
        decimal amount,
        string? reason,
        string refundType,
        CancellationToken cancellationToken = default);
}
