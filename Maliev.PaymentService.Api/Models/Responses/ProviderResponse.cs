using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Detailed provider response.
/// </summary>
public class ProviderResponse
{
    /// <summary>
    /// Provider unique identifier.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Provider unique name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Operational status.
    /// </summary>
    public required ProviderStatus Status { get; set; }

    /// <summary>
    /// Supported currencies.
    /// </summary>
    public required List<string> SupportedCurrencies { get; set; }

    /// <summary>
    /// Routing priority.
    /// </summary>
    public required int Priority { get; set; }

    /// <summary>
    /// Regional configurations.
    /// </summary>
    public required List<ProviderConfigurationDto> Configurations { get; set; }

    /// <summary>
    /// When the provider was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the provider was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Credentials are NEVER exposed in responses for security.
    /// </summary>
    public Dictionary<string, string>? Credentials => null;

    /// <summary>
    /// Regional configuration DTO.
    /// </summary>
    public class ProviderConfigurationDto
    {
        public required Guid Id { get; set; }
        public required string Region { get; set; }
        public required string ApiBaseUrl { get; set; }
        public required bool IsActive { get; set; }
        public required int MaxRetries { get; set; }
        public required int TimeoutSeconds { get; set; }
    }
}
