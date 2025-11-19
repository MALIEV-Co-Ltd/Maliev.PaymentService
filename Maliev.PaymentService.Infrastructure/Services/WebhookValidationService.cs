using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Service for validating webhook signatures from payment providers.
/// Delegates to provider-specific validators based on provider name.
/// </summary>
public class WebhookValidationService : IWebhookValidationService
{
    private readonly StripeWebhookValidator _stripeValidator;
    private readonly PayPalWebhookValidator _payPalValidator;
    private readonly OmiseWebhookValidator _omiseValidator;
    private readonly ScbWebhookValidator _scbValidator;
    private readonly ILogger<WebhookValidationService> _logger;

    public WebhookValidationService(ILogger<WebhookValidationService> logger)
    {
        _stripeValidator = new StripeWebhookValidator();
        _payPalValidator = new PayPalWebhookValidator();
        _omiseValidator = new OmiseWebhookValidator();
        _scbValidator = new ScbWebhookValidator();
        _logger = logger;
    }

    public Task<bool> ValidateWebhookAsync(
        PaymentProvider provider,
        string payload,
        Dictionary<string, string> headers,
        string? sourceIp = null)
    {
        if (provider == null || string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Invalid validation parameters: provider or payload is null");
            return Task.FromResult(false);
        }

        bool isValid = provider.Name.ToLowerInvariant() switch
        {
            "stripe" => ValidateStripeWebhook(payload, headers, provider),
            "paypal" => ValidatePayPalWebhook(payload, headers, provider),
            "omise" => ValidateOmiseWebhook(payload, headers, sourceIp, provider),
            "scb" => ValidateScbWebhook(payload, headers, provider),
            _ => false
        };

        if (!isValid)
        {
            _logger.LogWarning("Webhook validation failed for provider {ProviderName}", provider.Name);
        }

        return Task.FromResult(isValid);
    }

    private bool ValidateStripeWebhook(string payload, Dictionary<string, string> headers, PaymentProvider provider)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var signature))
        {
            _logger.LogWarning("Stripe webhook missing Stripe-Signature header");
            return false;
        }

        if (!provider.Credentials.TryGetValue("WebhookSecret", out var secret))
        {
            _logger.LogWarning("Stripe provider missing WebhookSecret credential");
            return false;
        }

        return _stripeValidator.ValidateSignature(payload, signature, secret);
    }

    private bool ValidatePayPalWebhook(string payload, Dictionary<string, string> headers, PaymentProvider provider)
    {
        if (!headers.TryGetValue("PAYPAL-TRANSMISSION-ID", out var transmissionId) ||
            !headers.TryGetValue("PAYPAL-TRANSMISSION-TIME", out var transmissionTime) ||
            !headers.TryGetValue("PAYPAL-TRANSMISSION-SIG", out var transmissionSig))
        {
            _logger.LogWarning("PayPal webhook missing required headers");
            return false;
        }

        headers.TryGetValue("PAYPAL-CERT-URL", out var certUrl);
        headers.TryGetValue("PAYPAL-AUTH-ALGO", out var authAlgo);

        if (!provider.Credentials.TryGetValue("WebhookId", out var webhookId))
        {
            _logger.LogWarning("PayPal provider missing WebhookId credential");
            return false;
        }

        return _payPalValidator.ValidateSignature(
            payload,
            transmissionId,
            transmissionTime,
            transmissionSig,
            certUrl ?? string.Empty,
            authAlgo ?? string.Empty,
            webhookId);
    }

    private bool ValidateOmiseWebhook(string payload, Dictionary<string, string> headers, string? sourceIp, PaymentProvider provider)
    {
        // Omise primarily uses IP whitelist validation
        if (!_omiseValidator.ValidateIpAddress(sourceIp))
        {
            _logger.LogWarning("Omise webhook from non-whitelisted IP: {SourceIp}", sourceIp);
            return false;
        }

        // Optional signature validation if configured
        if (headers.TryGetValue("X-Omise-Signature", out var signature) &&
            provider.Credentials.TryGetValue("WebhookSecret", out var secret))
        {
            return _omiseValidator.ValidateSignature(payload, signature, secret);
        }

        // If no signature, IP validation is sufficient
        return true;
    }

    private bool ValidateScbWebhook(string payload, Dictionary<string, string> headers, PaymentProvider provider)
    {
        if (!headers.TryGetValue("X-SCB-Signature", out var signature))
        {
            _logger.LogWarning("SCB webhook missing X-SCB-Signature header");
            return false;
        }

        if (!provider.Credentials.TryGetValue("WebhookSecret", out var secret))
        {
            _logger.LogWarning("SCB provider missing WebhookSecret credential");
            return false;
        }

        headers.TryGetValue("X-SCB-Timestamp", out var timestamp);
        headers.TryGetValue("X-SCB-Request-ID", out var requestId);

        return _scbValidator.ValidateSignature(payload, signature, secret, timestamp, requestId);
    }
}
