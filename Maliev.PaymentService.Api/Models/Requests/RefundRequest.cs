using System.Text.Json.Serialization;

namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request model for processing a refund.
/// </summary>
public class RefundRequest
{
    /// <summary>
    /// Refund amount (must be greater than 0 and less than or equal to remaining refundable amount).
    /// </summary>
    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// Reason for the refund (customer-facing).
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Type of refund: "full" or "partial".
    /// </summary>
    [JsonPropertyName("refundType")]
    public required string RefundType { get; init; }
}
