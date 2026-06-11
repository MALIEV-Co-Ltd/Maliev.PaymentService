using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Text.Json;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class WebhookProcessingServiceTests
{
    private readonly Mock<IWebhookRepository> _webhookRepositoryMock;
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IMetricsService> _metricsServiceMock;
    private readonly Mock<ILogger<WebhookProcessingService>> _loggerMock;
    private readonly WebhookProcessingService _service;

    public WebhookProcessingServiceTests()
    {
        _webhookRepositoryMock = new Mock<IWebhookRepository>();
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _metricsServiceMock = new Mock<IMetricsService>();
        _loggerMock = new Mock<ILogger<WebhookProcessingService>>();

        _service = new WebhookProcessingService(
            _webhookRepositoryMock.Object,
            _paymentRepositoryMock.Object,
            _eventPublisherMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Duplicate_ShouldReturnDuplicate()
    {
        var webhook = CreateTestWebhook();
        var existing = CreateTestWebhook();

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(webhook.ProviderId, webhook.ProviderEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.True(result.IsDuplicate);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PersistedCurrentEvent_ShouldNotTreatItselfAsDuplicate()
    {
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transactionId"] = Guid.NewGuid().ToString()
        });
        webhook.EventType = "payment.completed";
        var transaction = CreateTestTransaction(PaymentStatus.Processing);

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(webhook.ProviderId, webhook.ProviderEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhook);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<PaymentTransaction>(t => t.Status == PaymentStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidJsonPayload_ShouldContinue()
    {
        var webhook = CreateTestWebhook();
        webhook.RawPayload = "not-valid-json";

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentCompleted_ShouldPublishEvent()
    {
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["transactionId"] = Guid.NewGuid().ToString()
        });
        webhook.EventType = "payment.completed";

        var transaction = CreateTestTransaction(PaymentStatus.Processing);

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _eventPublisherMock
            .Setup(e => e.PublishAsync(It.IsAny<Maliev.MessagingContracts.Contracts.Payments.PaymentCompletedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(It.IsAny<Maliev.MessagingContracts.Contracts.Payments.PaymentCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentCompletedWithOrderNumberMetadata_ShouldPublishOrderNumber()
    {
        var transactionId = Guid.NewGuid();
        var orderNumber = "QE-20260611-0007";
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transactionId"] = transactionId.ToString()
        });
        webhook.EventType = "payment.completed";

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = orderNumber
        };

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<Maliev.MessagingContracts.Contracts.Payments.PaymentCompletedEvent>(paymentEvent =>
                    paymentEvent.Payload.OrderNumber == orderNumber &&
                    paymentEvent.Payload.OrderId.ToString() == transaction.OrderId &&
                    paymentEvent.Payload.CustomerId == transaction.CustomerId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StripeCheckoutSessionCompleted_UsesNestedMetadataTransactionId()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "checkout.session.completed";
        webhook.RawPayload = JsonSerializer.Serialize(new
        {
            id = "evt_checkout_completed",
            type = "checkout.session.completed",
            data = new
            {
                @object = new
                {
                    id = "cs_test_123",
                    metadata = new
                    {
                        transactionId = transactionId.ToString(),
                        orderNumber = "ORD-456"
                    }
                }
            }
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "ORD-456"
        };

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.Equal(transactionId, result.TransactionId);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<PaymentTransaction>(t => t.Status == PaymentStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StripeCheckoutSessionExpired_ShouldPublishPaymentFailedEvent()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "checkout.session.expired";
        webhook.RawPayload = JsonSerializer.Serialize(new
        {
            id = "evt_checkout_expired",
            type = "checkout.session.expired",
            data = new
            {
                @object = new
                {
                    id = "cs_test_expired",
                    metadata = new
                    {
                        transactionId = transactionId.ToString(),
                        orderNumber = "ORD-CANCEL-001"
                    }
                }
            }
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "ORD-CANCEL-001"
        };

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<Maliev.MessagingContracts.Contracts.Payments.PaymentFailedEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.CustomerId == transaction.CustomerId &&
                    paymentEvent.Payload.OrderId == transaction.OrderId &&
                    paymentEvent.Payload.Amount == (double)transaction.Amount &&
                    paymentEvent.Payload.Currency == transaction.Currency &&
                    paymentEvent.Payload.ErrorMessage.Contains("checkout.session.expired", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_SameStatus_ShouldNotUpdate()
    {
        var webhook = CreateTestWebhook();
        webhook.EventType = "payment.completed";

        var transaction = CreateTestTransaction(PaymentStatus.Completed);

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_TransactionNotFound_ShouldContinue()
    {
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["transactionId"] = Guid.NewGuid().ToString()
        });
        webhook.EventType = "payment.completed";

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RetryWebhookAsync_NotFound_ShouldReturnError()
    {
        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(Guid.Empty, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        var result = await _service.RetryWebhookAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Webhook event not found", result.ErrorMessage);
    }

    [Theory]
    [InlineData("payment.succeeded", PaymentStatus.Completed)]
    [InlineData("payment.success", PaymentStatus.Completed)]
    [InlineData("payment.failed", PaymentStatus.Failed)]
    [InlineData("payment.failure", PaymentStatus.Failed)]
    [InlineData("payment.declined", PaymentStatus.Failed)]
    [InlineData("payment.cancelled", PaymentStatus.Failed)]
    [InlineData("payment.canceled", PaymentStatus.Failed)]
    [InlineData("payment.pending", PaymentStatus.Processing)]
    [InlineData("payment.processing", PaymentStatus.Processing)]
    [InlineData("payment.refunded", PaymentStatus.Refunded)]
    [InlineData("unknown.event", PaymentStatus.Processing)]
    public async Task ProcessWebhookAsync_EventTypeMapping_ShouldMapCorrectly(string eventType, PaymentStatus expectedStatus)
    {
        var webhook = CreateTestWebhook();
        webhook.EventType = eventType;
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["transactionId"] = Guid.NewGuid().ToString()
        });

        var transaction = CreateTestTransaction(PaymentStatus.Pending);

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        _paymentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction t, CancellationToken _) => t);

        _paymentRepositoryMock
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.ProcessWebhookAsync(webhook);

        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<PaymentTransaction>(t => t.Status == expectedStatus),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static WebhookEvent CreateTestWebhook()
    {
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            ProviderEventId = "evt_123",
            EventType = "test.event",
            RawPayload = "{}",
            SignatureValidated = false,
            ProcessingStatus = WebhookProcessingStatus.Pending,
            ProcessingAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static PaymentTransaction CreateTestTransaction(PaymentStatus status)
    {
        return new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "test",
            Amount = 100,
            Currency = "THB",
            Status = status,
            CustomerId = "cust_123",
            OrderId = Guid.NewGuid().ToString(),
            Description = "Test",
            PaymentProviderId = Guid.NewGuid(),
            ProviderName = "stripe",
            ProviderTransactionId = "txn_123",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel",
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
