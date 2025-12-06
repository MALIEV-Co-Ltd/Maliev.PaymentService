using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Represents a payment transaction processed through the gateway.
/// Tracks the lifecycle of a payment from initiation to completion/failure.
/// </summary>
public class PaymentTransaction
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Idempotency key for duplicate detection.
    /// Combination of operation type + key ensures uniqueness.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD", "EUR").
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>
    /// Current payment status.
    /// </summary>
    public required PaymentStatus Status { get; set; }

    /// <summary>
    /// Customer identifier from the calling service.
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// Order/booking identifier from the calling service.
    /// </summary>
    public required string OrderId { get; set; }

    /// <summary>
    /// Payment description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Selected payment provider ID.
    /// </summary>
    public required Guid PaymentProviderId { get; set; }

    /// <summary>
    /// Selected payment provider name (denormalized for reporting).
    /// </summary>
    public required string ProviderName { get; set; }

    /// <summary>
    /// Provider's transaction identifier.
    /// </summary>
    public required string ProviderTransactionId { get; set; }

    /// <summary>
    /// Payment URL from provider (if applicable).
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Return URL for successful payment.
    /// </summary>
    public required string ReturnUrl { get; set; }

    /// <summary>
    /// Cancel URL for cancelled payment.
    /// </summary>
    public required string CancelUrl { get; set; }

    /// <summary>
    /// Custom metadata as JSON (e.g., booking details, campaign info).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Error message if payment failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Provider-specific error code.
    /// </summary>
    public string? ProviderErrorCode { get; set; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// When the transaction was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the transaction was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When the payment was completed (success or failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Navigation property to payment provider.
    /// </summary>
    public PaymentProvider? PaymentProvider { get; set; }

    /// <summary>
    /// Navigation property to transaction logs (audit trail).
    /// </summary>
    public List<TransactionLog> TransactionLogs { get; set; } = new();
}
