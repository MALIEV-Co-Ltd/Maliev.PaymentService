using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Services;
using Maliev.MessagingContracts.Contracts.Payments;
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
        transaction.ProviderName = "omise";
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

        PaymentCompletedEvent? publishedEvent = null;
        _eventPublisherMock
            .Setup(e => e.PublishAsync(It.IsAny<PaymentCompletedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentCompletedEvent, CancellationToken>((paymentEvent, _) => publishedEvent = paymentEvent)
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.NotNull(publishedEvent);
        Assert.Equal(orderNumber, publishedEvent.Payload.OrderNumber);
        Assert.Equal(transaction.OrderId, publishedEvent.Payload.OrderId.ToString());
        Assert.Equal(transaction.CustomerId, publishedEvent.Payload.CustomerId);
        Assert.Equal("omise", publishedEvent.Payload.ProviderName);
        Assert.Contains("InvoiceService", publishedEvent.ConsumedBy);
        Assert.Contains("OrderService", publishedEvent.ConsumedBy);
        Assert.Contains("NotificationService", publishedEvent.ConsumedBy);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentCompletedWithStringOrderId_ShouldPublishDeterministicOrderGuid()
    {
        var transactionId = Guid.NewGuid();
        var orderNumber = "ORD-2026-0042";
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transactionId"] = transactionId.ToString()
        });
        webhook.EventType = "payment.completed";

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.OrderId = orderNumber;
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

        PaymentCompletedEvent? publishedEvent = null;
        _eventPublisherMock
            .Setup(e => e.PublishAsync(It.IsAny<PaymentCompletedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentCompletedEvent, CancellationToken>((paymentEvent, _) => publishedEvent = paymentEvent)
            .Returns(Task.CompletedTask);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.NotNull(publishedEvent);
        Assert.Equal(CreateDeterministicGuid(orderNumber), publishedEvent.Payload.OrderId);
        Assert.Equal(orderNumber, publishedEvent.Payload.OrderNumber);
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
    public async Task ProcessWebhookAsync_OmiseChargeUpdateSuccessful_UsesNestedPayloadStatus()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "charge.update";
        webhook.RawPayload = JsonSerializer.Serialize(new
        {
            id = "evt_omise_charge_update",
            type = "charge.update",
            data = new
            {
                @object = new
                {
                    id = "chrg_test_123",
                    status = "successful",
                    metadata = new
                    {
                        transactionId = transactionId.ToString(),
                        orderNumber = "QE-20260612-0011"
                    }
                }
            }
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.ProviderName = "opn";
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "QE-20260612-0011"
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
                It.Is<PaymentTransaction>(t => t.Status == PaymentStatus.Completed && t.CompletedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<PaymentCompletedEvent>(paymentEvent =>
                    paymentEvent.Payload.PaymentId == transaction.Id &&
                    paymentEvent.Payload.ProviderName == "opn"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_OmiseChargeComplete_MapsEventTypeToCompleted()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "charge.complete";
        webhook.RawPayload = JsonSerializer.Serialize(new
        {
            id = "evt_omise_charge_complete",
            key = "charge.complete",
            data = new
            {
                id = "chrg_test_complete",
                metadata = new
                {
                    transactionId = transactionId.ToString(),
                    orderNumber = "QE-20260612-0012"
                }
            }
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.ProviderName = "omise";
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "QE-20260612-0012"
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
                It.Is<PaymentTransaction>(t => t.Status == PaymentStatus.Completed && t.CompletedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<PaymentCompletedEvent>(paymentEvent =>
                    paymentEvent.Payload.PaymentId == transaction.Id &&
                    paymentEvent.Payload.ProviderName == "omise"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("charge.update", "failed", PaymentStatus.Failed)]
    [InlineData("charge.update", "expired", PaymentStatus.Expired)]
    [InlineData("charge.update", "reversed", PaymentStatus.Cancelled)]
    [InlineData("charge.expire", null, PaymentStatus.Expired)]
    [InlineData("charge.reverse", null, PaymentStatus.Cancelled)]
    public async Task ProcessWebhookAsync_OmiseChargeEvents_MapDirectChargeDataStatus(
        string eventType,
        string? providerStatus,
        PaymentStatus expectedStatus)
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = eventType;
        webhook.RawPayload = JsonSerializer.Serialize(new
        {
            id = $"evt_omise_{eventType.Replace(".", "_", StringComparison.Ordinal)}",
            key = eventType,
            data = new
            {
                id = "chrg_test_status",
                status = providerStatus,
                metadata = new
                {
                    transactionId = transactionId.ToString()
                }
            }
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.ProviderName = "opn";

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
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<PaymentTransaction>(t => t.Status == expectedStatus),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_StripeCheckoutSessionExpired_ShouldPublishPaymentExpiredEvent()
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
                It.Is<PaymentExpiredEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.CustomerId == transaction.CustomerId &&
                    paymentEvent.Payload.OrderId == "ORD-CANCEL-001" &&
                    paymentEvent.Payload.Amount == (double)transaction.Amount &&
                    paymentEvent.Payload.Currency == transaction.Currency &&
                    paymentEvent.Payload.ProviderEventCode == "checkout.session.expired" &&
                    paymentEvent.ConsumedBy.Contains("QuoteEngine")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentCancelled_ShouldPublishPaymentCancelledEvent()
    {
        var webhook = CreateTestWebhook();
        webhook.EventType = "payment.cancelled";
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["transactionId"] = Guid.NewGuid().ToString()
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "ORD-CANCEL-002"
        };
        SetupSuccessfulStatusUpdate(transaction);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<PaymentCancelledEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.CustomerId == transaction.CustomerId &&
                    paymentEvent.ConsumedBy.Contains("QuoteEngine") &&
                    paymentEvent.Payload.OrderId == "ORD-CANCEL-002" &&
                    paymentEvent.Payload.ProviderEventCode == "payment.cancelled"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PaymentPending_ShouldPublishPaymentPendingEvent()
    {
        var webhook = CreateTestWebhook();
        webhook.EventType = "payment.pending";
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["transactionId"] = Guid.NewGuid().ToString()
        });

        var transaction = CreateTestTransaction(PaymentStatus.Pending);
        transaction.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "ORD-PENDING-001"
        };
        SetupSuccessfulStatusUpdate(transaction);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<PaymentPendingEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.CustomerId == transaction.CustomerId &&
                    paymentEvent.Payload.OrderId == "ORD-PENDING-001" &&
                    paymentEvent.Payload.ProviderEventCode == "payment.pending" &&
                    paymentEvent.ConsumedBy.Contains("QuoteEngine")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhookAsync_PendingLikeWebhookForProcessingTransaction_ShouldPublishPaymentPendingEvent()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "payment.processing";
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = "evt_processing",
            ["transactionId"] = transactionId.ToString()
        });

        var transaction = CreateTestTransaction(PaymentStatus.Processing);
        transaction.ProviderName = "opn";

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(
                It.Is<PaymentPendingEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.ProviderName == "opn" &&
                    paymentEvent.Payload.ProviderEventCode == "payment.processing"),
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
    public async Task ProcessWebhookAsync_CompletedTransactionReceivesLateFailure_ShouldNotDowngradeOrPublishFailure()
    {
        var transactionId = Guid.NewGuid();
        var webhook = CreateTestWebhook();
        webhook.EventType = "payment.failed";
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = "evt_late_failure",
            ["transactionId"] = transactionId.ToString()
        });

        var transaction = CreateTestTransaction(PaymentStatus.Completed);

        _webhookRepositoryMock
            .Setup(r => r.GetByProviderEventIdAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        _webhookRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _service.ProcessWebhookAsync(webhook);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Completed, transaction.Status);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _paymentRepositoryMock.Verify(
            r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhookAsync_TransactionNotFound_ShouldFailAndScheduleRetry()
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

        Assert.False(result.Success);
        Assert.Equal(WebhookProcessingStatus.Failed, webhook.ProcessingStatus);
        Assert.NotNull(webhook.FailedAt);
        Assert.NotNull(webhook.NextRetryAt);
        Assert.Contains("transaction", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryWebhookAsync_NotFound_ShouldReturnError()
    {
        _webhookRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WebhookEvent?)null);

        var result = await _service.RetryWebhookAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Webhook event not found", result.ErrorMessage);
    }

    [Fact]
    public async Task RetryWebhookAsync_CompletedWebhook_ShouldNotReprocessOrRepublish()
    {
        var webhook = CreateTestWebhook();
        webhook.ProcessingStatus = WebhookProcessingStatus.Completed;
        webhook.ProcessedAt = DateTime.UtcNow.AddMinutes(-5);
        webhook.PaymentTransactionId = Guid.NewGuid();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transactionId"] = webhook.PaymentTransactionId.Value.ToString()
        });
        webhook.EventType = "payment.completed";

        _webhookRepositoryMock
            .Setup(r => r.GetByIdAsync(webhook.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhook);

        var result = await _service.RetryWebhookAsync(webhook.Id);

        Assert.True(result.Success);
        Assert.False(result.IsDuplicate);
        Assert.Equal(webhook.PaymentTransactionId, result.TransactionId);
        _webhookRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _paymentRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _eventPublisherMock.Verify(
            e => e.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RetryWebhookAsync_ExistingWebhookId_ShouldProcessPersistedWebhook()
    {
        var webhook = CreateTestWebhook();
        webhook.RawPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["transactionId"] = Guid.NewGuid().ToString()
        });
        webhook.EventType = "payment.completed";
        var transaction = CreateTestTransaction(PaymentStatus.Processing);

        _webhookRepositoryMock
            .Setup(r => r.GetByIdAsync(webhook.Id, It.IsAny<CancellationToken>()))
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

        var result = await _service.RetryWebhookAsync(webhook.Id);

        Assert.True(result.Success);
        _paymentRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<PaymentTransaction>(t => t.Status == PaymentStatus.Completed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("payment.succeeded", PaymentStatus.Completed)]
    [InlineData("payment.success", PaymentStatus.Completed)]
    [InlineData("payment.failed", PaymentStatus.Failed)]
    [InlineData("payment.failure", PaymentStatus.Failed)]
    [InlineData("payment.declined", PaymentStatus.Failed)]
    [InlineData("payment.cancelled", PaymentStatus.Cancelled)]
    [InlineData("payment.canceled", PaymentStatus.Cancelled)]
    [InlineData("checkout.session.expired", PaymentStatus.Expired)]
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

    private void SetupSuccessfulStatusUpdate(PaymentTransaction transaction)
    {
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

    private static Guid CreateDeterministicGuid(string value)
    {
        byte[] hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }
}
