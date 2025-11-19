namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request to process a payment through the gateway.
/// </summary>
public class PaymentRequest
{
    /// <summary>
    /// Payment amount (must be greater than 0).
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
    /// Must be 3 uppercase characters.
    /// </summary>
    public required string Currency { get; set; }

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
    /// URL to redirect user after successful payment.
    /// </summary>
    public required string ReturnUrl { get; set; }

    /// <summary>
    /// URL to redirect user if payment is cancelled.
    /// </summary>
    public required string CancelUrl { get; set; }

    /// <summary>
    /// Optional metadata for the payment (e.g., booking details, campaign info).
    /// Stored as JSONB in database.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Optional specific provider to use (overrides routing logic).
    /// If not specified, provider is selected based on currency and priority.
    /// </summary>
    public string? PreferredProvider { get; set; }
}
