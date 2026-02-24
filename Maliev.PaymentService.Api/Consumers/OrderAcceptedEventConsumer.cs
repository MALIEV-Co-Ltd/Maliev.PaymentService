using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Generated;
using MassTransit;

namespace Maliev.PaymentService.Api.Consumers;

/// <summary>
/// Consumes OrderAcceptedEvent to track orders awaiting payment.
/// </summary>
public partial class OrderAcceptedEventConsumer : IConsumer<OrderAcceptedEvent>
{
    private readonly ILogger<OrderAcceptedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderAcceptedEventConsumer"/> class.
    /// </summary>
    public OrderAcceptedEventConsumer(ILogger<OrderAcceptedEventConsumer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Consumes the OrderAcceptedEvent.
    /// </summary>
    public async Task Consume(ConsumeContext<OrderAcceptedEvent> context)
    {
        var @event = context.Message;
        var payload = @event.Payload;

        Log.ConsumingOrderAcceptedEvent(_logger, payload.OrderId, payload.OrderNumber, payload.CustomerId);

        try
        {
            // Order accepted - awaiting payment
            // Payment should be initiated by customer through payment gateway
            // This consumer serves as notification/audit trail
            // Future enhancement: Could trigger payment reminder notifications
            Log.OrderAwaitingPayment(_logger, payload.OrderId, payload.OrderNumber);
        }
        catch (Exception ex)
        {
            Log.ErrorProcessingOrderAcceptedEvent(_logger, ex, payload.OrderId);
            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Consuming OrderAcceptedEvent for OrderId: {OrderId}, OrderNumber: {OrderNumber}, CustomerId: {CustomerId}")]
        public static partial void ConsumingOrderAcceptedEvent(ILogger logger, Guid orderId, string orderNumber, Guid customerId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} ({OrderNumber}) is now awaiting payment")]
        public static partial void OrderAwaitingPayment(ILogger logger, Guid orderId, string orderNumber);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error processing OrderAcceptedEvent for OrderId {OrderId}")]
        public static partial void ErrorProcessingOrderAcceptedEvent(ILogger logger, Exception ex, Guid orderId);
    }
}
