namespace Maliev.PaymentService.Core.Enums;

/// <summary>
/// Payment transaction status.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been created but not yet sent to provider.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment is currently being processed by the provider.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Payment has been successfully completed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Payment failed during processing.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Payment has been fully refunded.
    /// </summary>
    Refunded = 4,

    /// <summary>
    /// Payment has been partially refunded.
    /// </summary>
    PartiallyRefunded = 5
}
