using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Summary provider response for list endpoints.
/// </summary>
public class ProviderSummary
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
}
