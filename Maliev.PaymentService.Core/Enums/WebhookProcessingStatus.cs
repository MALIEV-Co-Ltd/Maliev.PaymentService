namespace Maliev.PaymentService.Core.Enums;

/// <summary>
/// Webhook event processing status.
/// </summary>
public enum WebhookProcessingStatus
{
    /// <summary>
    /// Webhook has been received but not yet processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Webhook is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Webhook has been successfully processed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Webhook processing failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Webhook is a duplicate and was ignored.
    /// </summary>
    Duplicate = 4
}
