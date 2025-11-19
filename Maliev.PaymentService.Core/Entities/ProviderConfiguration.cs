namespace Maliev.PaymentService.Core.Entities;

/// <summary>
/// Represents a regional configuration for a payment provider.
/// Allows providers to have different endpoints, settings, and credentials per region.
/// </summary>
public class ProviderConfiguration
{
    /// <summary>
    /// Unique identifier for this configuration.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Foreign key to the parent PaymentProvider.
    /// </summary>
    public required Guid PaymentProviderId { get; set; }

    /// <summary>
    /// Region identifier (e.g., "US", "EU", "APAC").
    /// </summary>
    public required string Region { get; set; }

    /// <summary>
    /// API base URL for this region (e.g., "https://api.stripe.com", "https://api-eu.stripe.com").
    /// </summary>
    public required string ApiBaseUrl { get; set; }

    /// <summary>
    /// Whether this regional configuration is active.
    /// </summary>
    public required bool IsActive { get; set; }

    /// <summary>
    /// Maximum number of retries for failed requests to this region.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Request timeout in seconds for this region.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timestamp when this configuration was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp of the last configuration update.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property to the parent PaymentProvider.
    /// </summary>
    public PaymentProvider? PaymentProvider { get; set; }
}
