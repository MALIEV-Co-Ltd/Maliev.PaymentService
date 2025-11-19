using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service for validating webhook signatures from payment providers.
/// Delegates to provider-specific validation logic.
/// </summary>
public interface IWebhookValidationService
{
    /// <summary>
    /// Validates a webhook signature based on the provider.
    /// </summary>
    /// <param name="provider">Payment provider that sent the webhook</param>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="headers">HTTP headers from the webhook request</param>
    /// <param name="sourceIp">Source IP address of the request</param>
    /// <returns>True if signature/source is valid</returns>
    Task<bool> ValidateWebhookAsync(
        PaymentProvider provider,
        string payload,
        Dictionary<string, string> headers,
        string? sourceIp = null);
}
