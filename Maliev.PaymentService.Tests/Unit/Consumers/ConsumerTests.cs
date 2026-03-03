using Maliev.MessagingContracts.Contracts.Invoices;
using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.PaymentService.Api.Consumers;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Consumers;

public class ConsumerTests
{
    private readonly Mock<ILogger<InvoiceCreatedEventConsumer>> _invoiceLoggerMock = new();
    private readonly Mock<ILogger<OrderAcceptedEventConsumer>> _orderLoggerMock = new();

    [Fact]
    public async Task InvoiceCreatedEventConsumer_Consume_Succeeds()
    {
        // Arrange
        _invoiceLoggerMock.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
        var consumer = new InvoiceCreatedEventConsumer(_invoiceLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<InvoiceCreatedEvent>>();
        var message = new InvoiceCreatedEvent
        {
            Payload = new InvoiceCreatedEventPayload
            {
                InvoiceId = Guid.NewGuid(),
                InvoiceNumber = "INV-001",
                CustomerId = Guid.NewGuid(),
                TotalAmount = 100.0,
                Currency = "USD"
            }
        };
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        // Verify logger was called
        _invoiceLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OrderAcceptedEventConsumer_Consume_Succeeds()
    {
        // Arrange
        _orderLoggerMock.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
        var consumer = new OrderAcceptedEventConsumer(_orderLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderAcceptedEvent>>();
        var message = new OrderAcceptedEvent
        {
            Payload = new OrderAcceptedEventPayload
            {
                OrderId = Guid.NewGuid(),
                OrderNumber = "ORD-001",
                CustomerId = Guid.NewGuid(),
                AcceptedAmount = 150.0,
                Currency = "EUR"
            }
        };
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        _orderLoggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvoiceCreatedEventConsumer_Consume_Error_Throws()
    {
        // Arrange
        var consumer = new InvoiceCreatedEventConsumer(_invoiceLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<InvoiceCreatedEvent>>();
        // Message will be null, causing exception in payload access
        contextMock.Setup(x => x.Message).Returns((InvoiceCreatedEvent)null!);

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => consumer.Consume(contextMock.Object));
    }

    [Fact]
    public async Task OrderAcceptedEventConsumer_Consume_Error_Throws()
    {
        // Arrange
        var consumer = new OrderAcceptedEventConsumer(_orderLoggerMock.Object);
        var contextMock = new Mock<ConsumeContext<OrderAcceptedEvent>>();
        contextMock.Setup(x => x.Message).Returns((OrderAcceptedEvent)null!);

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => consumer.Consume(contextMock.Object));
    }
}
