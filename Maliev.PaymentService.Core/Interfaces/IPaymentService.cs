using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service for orchestrating payment processing.
/// Handles idempotency, provider routing, resilience, and transaction logging.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Processes a payment request through the gateway.
    /// </summary>
    /// <param name="request">Payment processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed payment transaction</returns>
    Task<PaymentTransaction> ProcessPaymentAsync(PaymentProcessingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment transaction by its unique identifier.
    /// </summary>
    /// <param name="transactionId">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment transaction if found, null otherwise</returns>
    Task<PaymentTransaction?> GetPaymentByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for processing a payment.
/// </summary>
public class PaymentProcessingRequest
{
    public required string IdempotencyKey { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string CustomerId { get; set; }
    public required string OrderId { get; set; }
    public required string Description { get; set; }
    public required string ReturnUrl { get; set; }
    public required string CancelUrl { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public string? PreferredProvider { get; set; }
    public required string CorrelationId { get; set; }
}
