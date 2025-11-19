using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service interface for payment status queries with caching.
/// </summary>
public interface IPaymentStatusService
{
    /// <summary>
    /// Gets payment status by transaction ID with Redis caching.
    /// Cache TTL: 60 seconds for active transactions, 3600 seconds (1 hour) for terminal states.
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment transaction if found, null otherwise</returns>
    Task<PaymentTransaction?> GetPaymentStatusAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
