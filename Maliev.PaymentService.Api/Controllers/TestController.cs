using Maliev.MessagingContracts.Contracts;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// Controller for testing and simulation purposes.
/// Only available in non-production environments.
/// </summary>
[ApiController]
[Route("payment/v1/test")]
public class TestController : ControllerBase
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<TestController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestController"/> class.
    /// </summary>
    public TestController(IEventPublisher eventPublisher, ILogger<TestController> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Manually publishes a PaymentCompletedEvent for testing.
    /// </summary>
    [HttpPost("publish-payment-completed")]
    public async Task<IActionResult> PublishPaymentCompleted([FromBody] PublishPaymentRequest request)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, "Not allowed in production");
        }

        var payload = new PaymentCompletedEventPayload(
            OrderId: request.OrderId,
            PaymentId: request.PaymentId,
            Amount: request.Amount,
            Currency: request.Currency
        );

        var @event = new PaymentCompletedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentCompletedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0",
            PublishedBy: "PaymentService",
            ConsumedBy: new[] { "NotificationService" },
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: payload
        );

        await _eventPublisher.PublishAsync(@event);

        _logger.LogInformation("Test: Published PaymentCompletedEvent for Order {OrderId}", request.OrderId);

        return Ok(new { Message = "Event published successfully", EventId = @event.MessageId });
    }
}

/// <summary>
/// Request model for manually publishing a payment event.
/// </summary>
public class PublishPaymentRequest
{
    /// <summary>
    /// Associated order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Associated payment identifier.
    /// </summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; set; } = "THB";
}
