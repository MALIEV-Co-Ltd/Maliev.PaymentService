using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request to register a new payment provider.
/// </summary>
public class RegisterProviderRequest
{
    /// <summary>
    /// Provider unique name (e.g., "stripe", "paypal").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Initial operational status.
    /// </summary>
    public required ProviderStatus Status { get; set; }

    /// <summary>
    /// Supported ISO 4217 currency codes.
    /// </summary>
    public required List<string> SupportedCurrencies { get; set; }

    /// <summary>
    /// Provider routing priority (lower = higher priority).
    /// </summary>
    public required int Priority { get; set; }

    /// <summary>
    /// Provider credentials (API keys, secrets, etc.).
    /// Will be encrypted before storage.
    /// </summary>
    public required Dictionary<string, string> Credentials { get; set; }

    /// <summary>
    /// Regional configurations.
    /// </summary>
    public List<ProviderConfigurationDto> Configurations { get; set; } = new();

    /// <summary>
    /// Regional configuration DTO.
    /// </summary>
    public class ProviderConfigurationDto
    {
        /// <summary>
        /// Region identifier (e.g., "US", "EU").
        /// </summary>
        public required string Region { get; set; }

        /// <summary>
        /// API base URL for this region.
        /// </summary>
        public required string ApiBaseUrl { get; set; }

        /// <summary>
        /// Whether this configuration is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Maximum retries for this region.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Timeout in seconds for this region.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
