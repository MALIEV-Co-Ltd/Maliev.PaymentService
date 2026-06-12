using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Application.Interfaces;
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
    private readonly OmiseWebhookValidator _omiseValidator;
    private readonly ScbWebhookValidator _scbValidator;
    private readonly IEncryptionService? _encryptionService;
    private readonly ILogger<WebhookValidationService> _logger;

    public WebhookValidationService(ILogger<WebhookValidationService> logger, IEncryptionService? encryptionService = null)
    {
        _stripeValidator = new StripeWebhookValidator();
        _omiseValidator = new OmiseWebhookValidator();
        _scbValidator = new ScbWebhookValidator();
        _encryptionService = encryptionService;
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
            "omise" or "opn" => ValidateOmiseWebhook(payload, headers, provider),
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

        if (!TryGetCredential(provider, "WebhookSecret", out var secret))
        {
            _logger.LogWarning("Stripe provider missing WebhookSecret credential");
            return false;
        }

        return _stripeValidator.ValidateSignature(payload, signature, secret);
    }

    private bool ValidateOmiseWebhook(string payload, Dictionary<string, string> headers, PaymentProvider provider)
    {
        if (!headers.TryGetValue("Omise-Signature", out var signature))
        {
            _logger.LogWarning("Omise webhook missing Omise-Signature header");
            return false;
        }

        if (!TryGetCredential(provider, "WebhookSecret", out var secret))
        {
            _logger.LogWarning("Omise provider missing WebhookSecret credential");
            return false;
        }

        return _omiseValidator.ValidateSignature(payload, signature, secret);
    }

    private bool ValidateScbWebhook(string payload, Dictionary<string, string> headers, PaymentProvider provider)
    {
        if (!headers.TryGetValue("X-SCB-Signature", out var signature))
        {
            _logger.LogWarning("SCB webhook missing X-SCB-Signature header");
            return false;
        }

        if (!TryGetCredential(provider, "WebhookSecret", out var secret))
        {
            _logger.LogWarning("SCB provider missing WebhookSecret credential");
            return false;
        }

        headers.TryGetValue("X-SCB-Timestamp", out var timestamp);
        headers.TryGetValue("X-SCB-Request-ID", out var requestId);

        return _scbValidator.ValidateSignature(payload, signature, secret, timestamp, requestId);
    }

    private bool TryGetCredential(PaymentProvider provider, string key, out string value)
    {
        if (!provider.Credentials.TryGetValue(key, out var storedValue) ||
            string.IsNullOrWhiteSpace(storedValue))
        {
            value = string.Empty;
            return false;
        }

        if (_encryptionService is null)
        {
            value = storedValue;
            return true;
        }

        try
        {
            value = _encryptionService.Decrypt(storedValue);
            return true;
        }
        catch
        {
            value = storedValue;
            return true;
        }
    }
}
