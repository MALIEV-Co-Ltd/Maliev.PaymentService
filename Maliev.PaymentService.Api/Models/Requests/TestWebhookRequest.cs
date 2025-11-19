namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request for testing webhook processing in sandbox/development environments.
/// </summary>
public class TestWebhookRequest
{
    /// <summary>
    /// Type of webhook event to simulate (e.g., "payment.completed").
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// Transaction ID to update (optional).
    /// </summary>
    public Guid? TransactionId { get; set; }

    /// <summary>
    /// Custom payload data to include in the test webhook.
    /// </summary>
    public Dictionary<string, object>? CustomData { get; set; }
}
