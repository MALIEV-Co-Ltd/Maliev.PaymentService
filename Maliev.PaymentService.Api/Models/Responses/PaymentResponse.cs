using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response containing payment transaction details.
/// </summary>
public class PaymentResponse
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    public required Guid TransactionId { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    public required decimal Amount { get; set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public required string Currency { get; set; }

    /// <summary>
    /// Current payment status.
    /// </summary>
    public required PaymentStatus Status { get; set; }

    /// <summary>
    /// Customer identifier.
    /// </summary>
    public required string CustomerId { get; set; }

    /// <summary>
    /// Order identifier.
    /// </summary>
    public required string OrderId { get; set; }

    /// <summary>
    /// Payment description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Selected payment provider name.
    /// </summary>
    public required string SelectedProvider { get; set; }

    /// <summary>
    /// Provider's transaction identifier.
    /// </summary>
    public required string ProviderTransactionId { get; set; }

    /// <summary>
    /// Payment URL for user to complete payment (if applicable).
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Metadata associated with the payment.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Error message if payment failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Provider error code if payment failed.
    /// </summary>
    public string? ProviderErrorCode { get; set; }

    /// <summary>
    /// When the payment was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the payment was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When the payment was completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
