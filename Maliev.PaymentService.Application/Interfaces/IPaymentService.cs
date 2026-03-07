using Maliev.PaymentService.Domain.Entities;

namespace Maliev.PaymentService.Application.Interfaces;

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
    /// <summary>
    /// Idempotency key used to deduplicate payment operations.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Payment amount to process.
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>
    /// ISO currency code for the transaction.
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>
    /// Customer identifier from the caller domain.
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// Order identifier from the caller domain.
    /// </summary>
    public required string OrderId { get; set; }

    /// <summary>
    /// Human-readable payment description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Callback URL for successful payment completion.
    /// </summary>
    public required string ReturnUrl { get; set; }

    /// <summary>
    /// Callback URL for cancelled payment.
    /// </summary>
    public required string CancelUrl { get; set; }

    /// <summary>
    /// Optional metadata forwarded to provider integrations.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Optional preferred provider name.
    /// </summary>
    public string? PreferredProvider { get; set; }

    /// <summary>
    /// Correlation identifier used for distributed tracing.
    /// </summary>
    public required string CorrelationId { get; set; }
}
