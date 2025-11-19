namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Interface for payment provider adapters.
/// Each provider (Stripe, PayPal, etc.) implements this interface for standardized integration.
/// </summary>
public interface IPaymentProviderAdapter
{
    /// <summary>
    /// Provider name (e.g., "stripe", "paypal").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Processes a payment through the provider.
    /// </summary>
    /// <param name="request">Payment processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment processing result</returns>
    Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a payment from the provider.
    /// </summary>
    /// <param name="providerTransactionId">Provider's transaction identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment status result</returns>
    Task<ProviderPaymentStatus> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a refund through the provider.
    /// </summary>
    /// <param name="request">Refund processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refund processing result</returns>
    Task<ProviderRefundResult> ProcessRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates webhook signature from the provider.
    /// </summary>
    /// <param name="payload">Webhook payload</param>
    /// <param name="signature">Webhook signature</param>
    /// <param name="secret">Webhook secret</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    bool ValidateWebhookSignature(string payload, string signature, string secret);
}

/// <summary>
/// Request for processing a payment through a provider.
/// </summary>
public class ProviderPaymentRequest
{
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string CustomerId { get; set; }
    public required string OrderId { get; set; }
    public required string Description { get; set; }
    public required string ReturnUrl { get; set; }
    public required string CancelUrl { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Result from processing a payment through a provider.
/// </summary>
public class ProviderPaymentResult
{
    public required bool Success { get; set; }
    public required string ProviderTransactionId { get; set; }
    public required string Status { get; set; }
    public string? PaymentUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? RawResponse { get; set; }
}

/// <summary>
/// Payment status from provider.
/// </summary>
public class ProviderPaymentStatus
{
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Request for processing a refund through a provider.
/// </summary>
public class ProviderRefundRequest
{
    public required string ProviderTransactionId { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string Reason { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Result from processing a refund through a provider.
/// </summary>
public class ProviderRefundResult
{
    public required bool Success { get; set; }
    public required string ProviderRefundId { get; set; }
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}
