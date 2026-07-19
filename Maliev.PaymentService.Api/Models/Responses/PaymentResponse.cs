using System.Text.Json.Serialization;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response containing payment transaction details.
/// </summary>
public class PaymentResponse
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public required Guid TransactionId { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    [JsonPropertyName("amount")]
    public required decimal Amount { get; set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    [JsonPropertyName("currency")]
    public required string Currency { get; set; }

    /// <summary>
    /// Current payment status.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Customer identifier.
    /// </summary>
    [JsonPropertyName("customerId")]
    public required string CustomerId { get; set; }

    /// <summary>
    /// Order identifier.
    /// </summary>
    [JsonPropertyName("orderId")]
    public required string OrderId { get; set; }

    /// <summary>
    /// Payment description.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Selected payment provider name.
    /// </summary>
    [JsonPropertyName("selectedProvider")]
    public required string SelectedProvider { get; set; }

    /// <summary>
    /// Provider's transaction identifier.
    /// </summary>
    [JsonPropertyName("providerTransactionId")]
    public required string ProviderTransactionId { get; set; }

    /// <summary>
    /// Payment URL for user to complete payment (if applicable).
    /// </summary>
    [JsonPropertyName("paymentUrl")]
    public string? PaymentUrl { get; set; }

    /// <summary>Scannable QR image URL (e.g. PromptPay), when the payment method is QR-based.</summary>
    [JsonPropertyName("qrImageUrl")]
    public string? QrImageUrl { get; set; }

    /// <summary>Raw QR payload for clients that render the code themselves.</summary>
    [JsonPropertyName("qrRawData")]
    public string? QrRawData { get; set; }

    /// <summary>When the QR / charge source expires, if provided.</summary>
    [JsonPropertyName("qrExpiresAt")]
    public DateTime? QrExpiresAt { get; set; }

    /// <summary>Resolved payment method (e.g. "promptpay", "card").</summary>
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Metadata associated with the payment.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Error message if payment failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Provider error code if payment failed.
    /// </summary>
    [JsonPropertyName("providerErrorCode")]
    public string? ProviderErrorCode { get; set; }

    /// <summary>
    /// When the payment was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the payment was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When the payment was completed (if applicable).
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
