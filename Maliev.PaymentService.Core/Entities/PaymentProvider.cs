using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Represents a payment provider (e.g., Stripe, PayPal, Square).
/// Stores provider configuration, credentials, and operational status.
/// </summary>
public class PaymentProvider
{
    /// <summary>
    /// Unique identifier for the provider.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Provider unique name (e.g., "stripe", "paypal").
    /// Must be unique across all providers.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable display name (e.g., "Stripe Payments", "PayPal").
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Current operational status of the provider.
    /// </summary>
    public required ProviderStatus Status { get; set; }

    /// <summary>
    /// List of ISO 4217 currency codes supported by this provider (e.g., ["USD", "EUR", "GBP"]).
    /// </summary>
    public required List<string> SupportedCurrencies { get; set; }

    /// <summary>
    /// Provider routing priority (lower number = higher priority).
    /// Used for provider selection when multiple providers support the same currency.
    /// </summary>
    public required int Priority { get; set; }

    /// <summary>
    /// Provider-specific credentials (API keys, secrets, etc.).
    /// Stored encrypted at rest using CredentialEncryptionService.
    /// Key-value pairs are provider-specific (e.g., {"ApiKey": "sk_...", "WebhookSecret": "whsec_..."}).
    /// </summary>
    public required Dictionary<string, string> Credentials { get; set; }

    /// <summary>
    /// Multi-region configurations for this provider.
    /// Each configuration represents a different region or endpoint.
    /// </summary>
    public required List<ProviderConfiguration> Configurations { get; set; }

    /// <summary>
    /// Timestamp when the provider was registered.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp of the last provider update.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optional timestamp for soft delete.
    /// Null if provider is active, set when deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
