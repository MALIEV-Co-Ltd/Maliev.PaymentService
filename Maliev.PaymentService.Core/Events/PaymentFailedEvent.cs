namespace Maliev.PaymentService.Core.Events;

/// <summary>
/// Event published when a payment fails.
/// </summary>
public class PaymentFailedEvent
{
    public required Guid TransactionId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string CustomerId { get; set; }
    public required string OrderId { get; set; }
    public required string ProviderName { get; set; }
    public required string ErrorMessage { get; set; }
    public string? ProviderErrorCode { get; set; }
    public required DateTime FailedAt { get; set; }
    public required string CorrelationId { get; set; }
}
