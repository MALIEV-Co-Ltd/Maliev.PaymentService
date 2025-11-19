using System.Text.Json.Serialization;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response model for refund operations.
/// </summary>
public class RefundResponse
{
    /// <summary>
    /// Unique refund transaction identifier.
    /// </summary>
    [JsonPropertyName("refundId")]
    public required Guid RefundId { get; init; }

    /// <summary>
    /// ID of the payment being refunded.
    /// </summary>
    [JsonPropertyName("paymentTransactionId")]
    public required Guid PaymentTransactionId { get; init; }

    /// <summary>
    /// Refund amount.
    /// </summary>
    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }

    /// <summary>
    /// Current refund status.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Reason for the refund.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Type of refund: "full" or "partial".
    /// </summary>
    [JsonPropertyName("refundType")]
    public required string RefundType { get; init; }

    /// <summary>
    /// Provider's refund transaction ID.
    /// </summary>
    [JsonPropertyName("providerRefundId")]
    public string? ProviderRefundId { get; init; }

    /// <summary>
    /// Error message if refund failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When the refund was initiated.
    /// </summary>
    [JsonPropertyName("initiatedAt")]
    public DateTime? InitiatedAt { get; init; }

    /// <summary>
    /// When the refund was completed.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// When the refund was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the refund was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }
}
