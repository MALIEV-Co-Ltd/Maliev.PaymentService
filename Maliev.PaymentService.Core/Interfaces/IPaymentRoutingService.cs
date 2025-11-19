using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service for routing payments to appropriate payment providers.
/// Implements currency-based routing with provider priority and circuit breaker awareness.
/// </summary>
public interface IPaymentRoutingService
{
    /// <summary>
    /// Selects the best payment provider for a given currency.
    /// </summary>
    /// <param name="currency">ISO 4217 currency code</param>
    /// <param name="preferredProvider">Optional preferred provider name (overrides routing logic)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Selected payment provider</returns>
    /// <exception cref="InvalidOperationException">Thrown when no suitable provider is available</exception>
    Task<PaymentProvider> SelectProviderAsync(string currency, string? preferredProvider = null, CancellationToken cancellationToken = default);
}
