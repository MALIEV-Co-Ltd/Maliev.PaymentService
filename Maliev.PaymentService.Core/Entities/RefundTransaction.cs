using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Represents a refund operation for a completed payment.
/// Tracks both full and partial refunds with idempotency support.
/// </summary>
public class RefundTransaction
{
    /// <summary>
    /// Unique refund identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// ID of the payment transaction being refunded.
    /// </summary>
    public required Guid PaymentTransactionId { get; set; }

    /// <summary>
    /// Provider ID that processes the refund.
    /// </summary>
    public required Guid ProviderId { get; set; }

    /// <summary>
    /// Provider's refund transaction identifier.
    /// </summary>
    public string? ProviderRefundId { get; set; }

    /// <summary>
    /// Refund amount (must be > 0 and <= remaining refundable amount).
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (must match payment currency).
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>
    /// Current refund status.
    /// </summary>
    public required RefundStatus Status { get; set; }

    /// <summary>
    /// Reason for the refund (customer-facing).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Type of refund: "full" or "partial".
    /// </summary>
    public required string RefundType { get; set; }

    /// <summary>
    /// Idempotency key for duplicate detection.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Provider-specific error code if refund failed.
    /// </summary>
    public string? ProviderErrorCode { get; set; }

    /// <summary>
    /// Error message if refund failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Internal notes (not shown to customer).
    /// </summary>
    public string? InternalNotes { get; set; }

    /// <summary>
    /// When the refund was initiated.
    /// </summary>
    public DateTime? InitiatedAt { get; set; }

    /// <summary>
    /// When the refund completed successfully.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When the refund failed.
    /// </summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public required Guid CorrelationId { get; set; }

    /// <summary>
    /// Whether this record is archived.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// When the refund was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the refund was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// User/service that created the refund.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// User/service that last updated the refund.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// </summary>
    public required byte[] RowVersion { get; set; }

    /// <summary>
    /// Navigation property to parent payment transaction.
    /// </summary>
    public PaymentTransaction? PaymentTransaction { get; set; }

    /// <summary>
    /// Navigation property to payment provider.
    /// </summary>
    public PaymentProvider? Provider { get; set; }

    /// <summary>
    /// Navigation property to transaction logs (audit trail).
    /// </summary>
    public List<TransactionLog> TransactionLogs { get; set; } = new();
}
