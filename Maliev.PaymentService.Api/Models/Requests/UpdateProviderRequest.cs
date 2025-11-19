using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request to update an existing payment provider.
/// </summary>
public class UpdateProviderRequest
{
    /// <summary>
    /// Updated display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Updated operational status.
    /// </summary>
    public ProviderStatus? Status { get; set; }

    /// <summary>
    /// Updated supported currencies.
    /// </summary>
    public List<string>? SupportedCurrencies { get; set; }

    /// <summary>
    /// Updated routing priority.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Updated credentials (if provided, will replace and re-encrypt).
    /// </summary>
    public Dictionary<string, string>? Credentials { get; set; }
}
