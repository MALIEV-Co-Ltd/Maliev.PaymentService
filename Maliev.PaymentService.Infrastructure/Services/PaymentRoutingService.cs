using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
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
        var isThailandCurrency = IsThailandCurrency(currency);

        // If preferred provider is specified, try to use it unless Thailand-local routing must stay primary.
        if (!string.IsNullOrEmpty(preferredProvider) &&
            (!isThailandCurrency || IsThailandPrimaryProvider(preferredProvider)))
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

        // Get all routable providers supporting the currency, ordered by priority
        var availableProviders = (await _providerRepository.GetRoutableByCurrencyAsync(currency, cancellationToken))
            .OrderBy(p => p.Priority)
            .ToList();

        if (!availableProviders.Any())
        {
            throw new InvalidOperationException($"No active payment providers available for currency {currency}");
        }

        // Filter out providers with open circuit breakers. Degraded providers remain routable,
        // but only after active providers have been exhausted.
        var circuitClosedProviders = availableProviders
            .Where(p => !_circuitBreakerStateManager.IsCircuitOpen(p.Name))
            .ToList();
        var healthyProviders = circuitClosedProviders
            .Where(p => p.Status == ProviderStatus.Active)
            .ToList();

        if (!healthyProviders.Any())
        {
            _logger.LogWarning("No active providers for currency {Currency} are available, checking degraded providers",
                currency);

            // Fall back to degraded providers only when active providers are unavailable.
            var degradedProviders = circuitClosedProviders
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

        if (isThailandCurrency)
        {
            var thailandPrimaryProvider = healthyProviders.FirstOrDefault(p => IsThailandPrimaryProvider(p.Name));
            if (thailandPrimaryProvider is not null)
            {
                _logger.LogInformation(
                    "Selected Thailand primary provider: {ProviderName} for currency {Currency}",
                    thailandPrimaryProvider.Name,
                    currency);
                return thailandPrimaryProvider;
            }
        }

        // Select provider with highest priority (lowest priority number)
        var selected = healthyProviders.First();

        _logger.LogInformation("Selected provider: {ProviderName} (priority: {Priority}) for currency {Currency}",
            selected.Name, selected.Priority, currency);

        return selected;
    }

    private static bool IsThailandCurrency(string currency)
    {
        return currency.Equals("THB", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThailandPrimaryProvider(string providerName)
    {
        return providerName.Equals("omise", StringComparison.OrdinalIgnoreCase) ||
               providerName.Equals("opn", StringComparison.OrdinalIgnoreCase);
    }
}
