using Maliev.MessagingContracts.Generated;
using MassTransit;

namespace Maliev.PaymentService.Api.Consumers;

/// <summary>
/// Consumes InvoiceCreatedEvent to link invoices to payments.
/// </summary>
public partial class InvoiceCreatedEventConsumer : IConsumer<InvoiceCreatedEvent>
{
    private readonly ILogger<InvoiceCreatedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceCreatedEventConsumer"/> class.
    /// </summary>
    public InvoiceCreatedEventConsumer(ILogger<InvoiceCreatedEventConsumer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Consumes the InvoiceCreatedEvent.
    /// </summary>
    public async Task Consume(ConsumeContext<InvoiceCreatedEvent> context)
    {
        var @event = context.Message;
        var payload = @event.Payload;

        Log.ConsumingInvoiceCreatedEvent(_logger, payload.InvoiceId, payload.InvoiceNumber, payload.CustomerId);

        try
        {
            // Invoice created - can be linked to payment intent/request
            // Payment gateway should reference invoiceId when creating payment
            // This consumer serves as notification/audit trail
            // Future enhancement: Could create payment intent automatically
            Log.InvoiceAvailableForPayment(_logger, payload.InvoiceId, payload.InvoiceNumber, payload.TotalAmount, payload.Currency);
        }
        catch (Exception ex)
        {
            Log.ErrorProcessingInvoiceCreatedEvent(_logger, ex, payload.InvoiceId);
            throw; // Re-throw to trigger MassTransit retry
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Consuming InvoiceCreatedEvent for InvoiceId: {InvoiceId}, InvoiceNumber: {InvoiceNumber}, CustomerId: {CustomerId}")]
        public static partial void ConsumingInvoiceCreatedEvent(ILogger logger, Guid invoiceId, string invoiceNumber, Guid customerId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} ({InvoiceNumber}) available for payment - Amount: {Amount} {Currency}")]
        public static partial void InvoiceAvailableForPayment(ILogger logger, Guid invoiceId, string invoiceNumber, double amount, string currency);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error processing InvoiceCreatedEvent for InvoiceId {InvoiceId}")]
        public static partial void ErrorProcessingInvoiceCreatedEvent(ILogger logger, Exception ex, Guid invoiceId);
    }
}
