namespace Maliev.PaymentService.Core.Events;

/// <summary>
/// Event published when a payment is created.
/// </summary>
public class PaymentCreatedEvent
{
    public required Guid TransactionId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string CustomerId { get; set; }
    public required string OrderId { get; set; }
    public required string ProviderName { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string CorrelationId { get; set; }
}
