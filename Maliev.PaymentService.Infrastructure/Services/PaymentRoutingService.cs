using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Payment routing service implementation.
/// Routes payments to providers based on currency, priority, and circuit breaker status.
/// </summary>
public class PaymentRoutingService : IPaymentRoutingService
{
    private readonly IProviderRepository _providerRepository;
    private readonly CircuitBreakerStateManager _circuitBreakerStateManager;
    private readonly ILogger<PaymentRoutingService> _logger;

    public PaymentRoutingService(
        IProviderRepository providerRepository,
        CircuitBreakerStateManager circuitBreakerStateManager,
        ILogger<PaymentRoutingService> logger)
    {
        _providerRepository = providerRepository;
        _circuitBreakerStateManager = circuitBreakerStateManager;
        _logger = logger;
    }

    public async Task<PaymentProvider> SelectProviderAsync(string currency, string? preferredProvider = null, CancellationToken cancellationToken = default)
    {
        // If preferred provider is specified, try to use it
        if (!string.IsNullOrEmpty(preferredProvider))
        {
            var providers = await _providerRepository.GetActiveByCurrencyAsync(currency, cancellationToken);
            var provider = providers.FirstOrDefault(p =>
                p.Name.Equals(preferredProvider, StringComparison.OrdinalIgnoreCase) &&
                p.Status == ProviderStatus.Active);

            if (provider != null)
            {
                // Check circuit breaker status
                if (!_circuitBreakerStateManager.IsCircuitOpen(provider.Name))
                {
                    _logger.LogInformation("Selected preferred provider: {ProviderName} for currency {Currency}",
                        provider.Name, currency);
                    return provider;
                }

                _logger.LogWarning("Preferred provider {ProviderName} has circuit breaker open, falling back to routing",
                    provider.Name);
            }
        }

        // Get all active providers supporting the currency, ordered by priority
        var availableProviders = await _providerRepository.GetActiveByCurrencyAsync(currency, cancellationToken);

        if (!availableProviders.Any())
        {
            throw new InvalidOperationException($"No active payment providers available for currency {currency}");
        }

        // Filter out providers with open circuit breakers
        var healthyProviders = availableProviders
            .Where(p => !_circuitBreakerStateManager.IsCircuitOpen(p.Name))
            .ToList();

        if (!healthyProviders.Any())
        {
            _logger.LogWarning("All providers for currency {Currency} have circuit breakers open, using degraded provider",
                currency);

            // Fall back to degraded providers if all healthy ones are circuit-broken
            var degradedProviders = availableProviders
                .Where(p => p.Status == ProviderStatus.Degraded)
                .ToList();

            if (degradedProviders.Any())
            {
                var selectedProvider = degradedProviders.First();
                _logger.LogInformation("Selected degraded provider: {ProviderName} for currency {Currency}",
                    selectedProvider.Name, currency);
                return selectedProvider;
            }

            throw new InvalidOperationException($"No healthy payment providers available for currency {currency} (all circuit breakers open)");
        }

        // Select provider with highest priority (lowest priority number)
        var selected = healthyProviders.First();

        _logger.LogInformation("Selected provider: {ProviderName} (priority: {Priority}) for currency {Currency}",
            selected.Name, selected.Priority, currency);

        return selected;
    }
}
