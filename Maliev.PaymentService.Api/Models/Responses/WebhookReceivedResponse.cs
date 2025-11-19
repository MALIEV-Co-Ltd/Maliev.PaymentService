namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response returned when a webhook is received and queued for processing.
/// </summary>
public class WebhookReceivedResponse
{
    /// <summary>
    /// Unique identifier for the webhook event.
    /// </summary>
    public required Guid WebhookEventId { get; init; }

    /// <summary>
    /// Whether the webhook was accepted for processing.
    /// </summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Whether this webhook was a duplicate (already processed).
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// Message describing the result.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Timestamp when the webhook was received.
    /// </summary>
    public required DateTime ReceivedAt { get; init; }
}
