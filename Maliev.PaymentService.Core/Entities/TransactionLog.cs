using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Immutable audit log for payment transaction state changes.
/// Each state transition creates a new log entry for full auditability.
/// </summary>
public class TransactionLog
{
    /// <summary>
    /// Unique log entry identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Foreign key to the payment transaction.
    /// </summary>
    public required Guid PaymentTransactionId { get; init; }

    /// <summary>
    /// Previous payment status (null for initial creation).
    /// </summary>
    public PaymentStatus? PreviousStatus { get; init; }

    /// <summary>
    /// New payment status after this change.
    /// </summary>
    public required PaymentStatus NewStatus { get; init; }

    /// <summary>
    /// Event type that triggered this log entry (e.g., "PaymentCreated", "StatusUpdated", "WebhookReceived").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Detailed message describing the state change.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Provider response data (stored as JSON).
    /// </summary>
    public string? ProviderResponse { get; init; }

    /// <summary>
    /// Error details if this was a failure event.
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// When this log entry was created (immutable timestamp).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Navigation property to payment transaction.
    /// </summary>
    public PaymentTransaction? PaymentTransaction { get; init; }
}
