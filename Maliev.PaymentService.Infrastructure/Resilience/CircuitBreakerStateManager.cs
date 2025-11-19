using System.Collections.Concurrent;

namespace Maliev.PaymentService.Infrastructure.Resilience;

/// <summary>
/// Manages circuit breaker states for payment providers.
/// Tracks which providers have circuit breakers open/closed.
/// </summary>
public class CircuitBreakerStateManager
{
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _providerStates = new();

    /// <summary>
    /// Records a circuit breaker state change for a provider.
    /// </summary>
    public void RecordStateChange(string providerName, bool isOpen, DateTime timestamp)
    {
        _providerStates.AddOrUpdate(
            providerName,
            new CircuitBreakerState
            {
                ProviderName = providerName,
                IsOpen = isOpen,
                LastStateChange = timestamp,
                StateChangeCount = 1
            },
            (key, existing) => new CircuitBreakerState
            {
                ProviderName = providerName,
                IsOpen = isOpen,
                LastStateChange = timestamp,
                StateChangeCount = existing.StateChangeCount + 1
            });
    }

    /// <summary>
    /// Gets the circuit breaker state for a provider.
    /// </summary>
    public CircuitBreakerState? GetState(string providerName)
    {
        _providerStates.TryGetValue(providerName, out var state);
        return state;
    }

    /// <summary>
    /// Gets all circuit breaker states.
    /// </summary>
    public IDictionary<string, CircuitBreakerState> GetAllStates()
    {
        return new Dictionary<string, CircuitBreakerState>(_providerStates);
    }

    /// <summary>
    /// Checks if a provider's circuit breaker is currently open.
    /// </summary>
    public bool IsCircuitOpen(string providerName)
    {
        return _providerStates.TryGetValue(providerName, out var state) && state.IsOpen;
    }
}

/// <summary>
/// Represents the circuit breaker state for a payment provider.
/// </summary>
public class CircuitBreakerState
{
    /// <summary>
    /// Provider name.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Whether the circuit breaker is currently open (provider unavailable).
    /// </summary>
    public bool IsOpen { get; init; }

    /// <summary>
    /// Timestamp of the last state change.
    /// </summary>
    public DateTime LastStateChange { get; init; }

    /// <summary>
    /// Number of times the circuit breaker has changed state.
    /// </summary>
    public int StateChangeCount { get; init; }
}
