using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Represents a webhook event received from a payment provider.
/// Stores the webhook payload, validation status, and processing state.
/// </summary>
public class WebhookEvent
{
    /// <summary>
    /// Unique identifier for this webhook event.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Foreign key to the payment provider that sent this webhook.
    /// </summary>
    public required Guid ProviderId { get; set; }

    /// <summary>
    /// Provider's unique event identifier (used for deduplication).
    /// </summary>
    public required string ProviderEventId { get; set; }

    /// <summary>
    /// Type of event (e.g., "payment.completed", "payment.failed").
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// Related payment transaction if applicable.
    /// </summary>
    public Guid? PaymentTransactionId { get; set; }

    /// <summary>
    /// Related refund transaction if applicable.
    /// </summary>
    public Guid? RefundTransactionId { get; set; }

    /// <summary>
    /// Complete webhook payload as received (raw text).
    /// </summary>
    public required string RawPayload { get; set; }

    /// <summary>
    /// Parsed webhook data as JSON (for querying).
    /// </summary>
    public Dictionary<string, object>? ParsedPayload { get; set; }

    /// <summary>
    /// Webhook signature for validation.
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Whether signature validation passed.
    /// </summary>
    public required bool SignatureValidated { get; set; }

    /// <summary>
    /// Source IP address of webhook request.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent from webhook request.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Current processing status of the webhook.
    /// </summary>
    public required WebhookProcessingStatus ProcessingStatus { get; set; }

    /// <summary>
    /// Number of processing attempts made.
    /// </summary>
    public required int ProcessingAttempts { get; set; }

    /// <summary>
    /// UTC timestamp when successfully processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// UTC timestamp when processing failed.
    /// </summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>
    /// Reason for processing failure.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Scheduled time for next retry attempt.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Correlation ID if event relates to a transaction.
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// UTC timestamp when webhook was received.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// UTC timestamp of last update.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Navigation property to payment provider.
    /// </summary>
    public PaymentProvider? PaymentProvider { get; set; }

    /// <summary>
    /// Navigation property to payment transaction.
    /// </summary>
    public PaymentTransaction? PaymentTransaction { get; set; }
}
