namespace Maliev.PaymentService.Core.Enums;

/// <summary>
/// Refund transaction status.
/// </summary>
public enum RefundStatus
{
    /// <summary>
    /// Refund has been created but not yet sent to provider.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Refund is currently being processed by the provider.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Refund has been successfully completed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Refund failed during processing.
    /// </summary>
    Failed = 3
}
