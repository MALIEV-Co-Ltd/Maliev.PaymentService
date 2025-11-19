using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request to update a provider's operational status.
/// </summary>
public class UpdateProviderStatusRequest
{
    /// <summary>
    /// New operational status for the provider.
    /// </summary>
    public required ProviderStatus Status { get; set; }
}
