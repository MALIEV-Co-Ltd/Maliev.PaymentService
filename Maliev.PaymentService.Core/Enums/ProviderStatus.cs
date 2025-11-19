namespace Maliev.PaymentService.Core.Enums;

/// <summary>
/// Payment provider operational status.
/// </summary>
public enum ProviderStatus
{
    /// <summary>
    /// Provider is active and accepting payment requests.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Provider has been manually disabled and will not receive requests.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// Provider is experiencing issues but may still accept requests.
    /// Circuit breaker may be active.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Provider is under scheduled maintenance.
    /// </summary>
    Maintenance = 3
}
