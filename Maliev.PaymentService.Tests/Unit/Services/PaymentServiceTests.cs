using System.Net;
using System.Net.Http.Json;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Providers;
using Maliev.PaymentService.Infrastructure.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using PaymentOrchestrationService = Maliev.PaymentService.Infrastructure.Services.PaymentService;

namespace Maliev.PaymentService.Tests.Unit.Services;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ProcessPaymentAsync_WhenOrderAlreadyCompleted_ReturnsExistingTransactionWithoutCallingProvider()
    {
        var provider = CreateStripeProvider();
        var completedTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "previous-attempt",
            Amount = 2500m,
            Currency = "THB",
            Status = PaymentStatus.Completed,
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            PaymentProviderId = provider.Id,
            ProviderName = provider.Name,
            ProviderTransactionId = "chrg_existing",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("new-attempt", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.GetLatestCompletedByOrderIdAsync("order-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedTransaction);

        var routing = new Mock<IPaymentRoutingService>();
        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "new-attempt", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var encryption = new Mock<IEncryptionService>();

        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            new ProviderFactory(httpClientFactory.Object, encryption.Object),
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var request = new PaymentProcessingRequest
        {
            IdempotencyKey = "new-attempt",
            Amount = 2500m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-456" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var transaction = await service.ProcessPaymentAsync(request);

        Assert.Same(completedTransaction, transaction);
        Assert.True(request.ExistingTransactionReturned);
        routing.Verify(
            r => r.SelectProviderAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(
            r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ForStripeAddsInternalTransactionIdToProviderMetadata()
    {
        var stripeHandler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "cs_test_orchestrated",
                url = "https://checkout.stripe.com/c/pay/cs_test_orchestrated",
                status = "open"
            })
        });

        var provider = CreateStripeProvider();
        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("idem-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);

        var routing = new Mock<IPaymentRoutingService>();
        routing
            .Setup(r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "idem-123", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("stripe"))
            .Returns(new HttpClient(stripeHandler));

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt("sk_test_123")).Returns("sk_test_123");

        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            new ProviderFactory(httpClientFactory.Object, encryption.Object),
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var transaction = await service.ProcessPaymentAsync(new PaymentProcessingRequest
        {
            IdempotencyKey = "idem-123",
            Amount = 2500m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-456" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        });

        var form = ReadForm(stripeHandler.RequestBody);
        Assert.NotNull(stripeHandler.Request);
        Assert.True(stripeHandler.Request.Headers.TryGetValues("Idempotency-Key", out var idempotencyValues));
        Assert.Equal("idem-123", Assert.Single(idempotencyValues));
        Assert.Equal(transaction.Id.ToString(), form["metadata[transactionId]"]);
        Assert.Equal(transaction.Id.ToString(), form["payment_intent_data[metadata][transactionId]"]);
        Assert.Equal("ORD-456", form["metadata[orderNumber]"]);
        Assert.Equal("customer-123", form["metadata[customerId]"]);
        Assert.Equal("order-456", form["metadata[orderId]"]);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ProviderAcceptsPayment_PublishesPaymentPendingEvent()
    {
        var stripeHandler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "cs_test_pending",
                url = "https://checkout.stripe.com/c/pay/cs_test_pending",
                status = "open"
            })
        });

        var provider = CreateStripeProvider();
        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("idem-pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);

        var routing = new Mock<IPaymentRoutingService>();
        routing
            .Setup(r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "idem-pending", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("stripe"))
            .Returns(new HttpClient(stripeHandler));

        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(e => e.Decrypt("sk_test_123")).Returns("sk_test_123");

        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            new ProviderFactory(httpClientFactory.Object, encryption.Object),
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var transaction = await service.ProcessPaymentAsync(new PaymentProcessingRequest
        {
            IdempotencyKey = "idem-pending",
            Amount = 1250m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-456" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        });

        events.Verify(
            e => e.PublishAsync(
                It.Is<PaymentPendingEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.IdempotencyKey == "idem-pending" &&
                    paymentEvent.Payload.Amount == 1250d &&
                    paymentEvent.Payload.Currency == "THB" &&
                    paymentEvent.Payload.CustomerId == "customer-123" &&
                    paymentEvent.Payload.OrderId == "ORD-456" &&
                    paymentEvent.Payload.ProviderName == "stripe" &&
                    paymentEvent.Payload.ProviderEventCode == "ProviderSuccess"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenThailandPrimaryProviderFails_FallsBackToStripe()
    {
        var omise = CreateOmiseProvider();
        var stripe = CreateStripeProvider();
        var omiseAdapter = new FakePaymentProviderAdapter(
            "omise",
            new ProviderPaymentResult
            {
                Success = false,
                ProviderTransactionId = "chrg_failed",
                Status = "failed",
                ErrorMessage = "Omise temporarily unavailable",
                ErrorCode = "provider_unavailable"
            });
        var stripeAdapter = new FakePaymentProviderAdapter(
            "stripe",
            new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = "cs_fallback",
                Status = "open",
                PaymentUrl = "https://checkout.stripe.com/c/pay/cs_fallback"
            });

        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("idem-fallback", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.GetLatestCompletedByOrderIdAsync("order-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var routing = new Mock<IPaymentRoutingService>();
        routing
            .SetupSequence(r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(omise)
            .ReturnsAsync(stripe);

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "idem-fallback", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var encryption = new Mock<IEncryptionService>();
        var providerFactory = new Mock<ProviderFactory>(httpClientFactory.Object, encryption.Object);
        providerFactory
            .Setup(f => f.CreateProvider(omise, null))
            .Returns(omiseAdapter);
        providerFactory
            .Setup(f => f.CreateProvider(stripe, null))
            .Returns(stripeAdapter);

        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            providerFactory.Object,
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var transaction = await service.ProcessPaymentAsync(new PaymentProcessingRequest
        {
            IdempotencyKey = "idem-fallback",
            Amount = 1250m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-456" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        });

        Assert.Equal("stripe", transaction.ProviderName);
        Assert.Equal(stripe.Id, transaction.PaymentProviderId);
        Assert.Equal("cs_fallback", transaction.ProviderTransactionId);
        Assert.Equal(PaymentStatus.Processing, transaction.Status);
        Assert.Equal(1, omiseAdapter.ProcessPaymentCallCount);
        Assert.Equal(1, stripeAdapter.ProcessPaymentCallCount);
        routing.Verify(
            r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        events.Verify(
            e => e.PublishAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        events.Verify(
            e => e.PublishAsync(
                It.Is<PaymentPendingEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.ProviderName == "stripe" &&
                    paymentEvent.Payload.ProviderEventCode == "ProviderSuccess"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenProviderFailureHasNoFallback_PublishesFailedEventWithOrderNumberForDownstreamServices()
    {
        var stripe = CreateStripeProvider();
        var stripeAdapter = new FakePaymentProviderAdapter(
            "stripe",
            new ProviderPaymentResult
            {
                Success = false,
                ProviderTransactionId = "cs_failed",
                Status = "failed",
                ErrorMessage = "Card declined",
                ErrorCode = "card_declined"
            });
        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("idem-failed", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.GetLatestCompletedByOrderIdAsync("order-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var routing = new Mock<IPaymentRoutingService>();
        routing
            .Setup(r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stripe);
        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "idem-failed", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var encryption = new Mock<IEncryptionService>();
        var providerFactory = new Mock<ProviderFactory>(httpClientFactory.Object, encryption.Object);
        providerFactory
            .Setup(f => f.CreateProvider(stripe, null))
            .Returns(stripeAdapter);
        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            providerFactory.Object,
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var transaction = await service.ProcessPaymentAsync(new PaymentProcessingRequest
        {
            IdempotencyKey = "idem-failed",
            Amount = 1250m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-456" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        });

        events.Verify(
            e => e.PublishAsync(
                It.Is<PaymentFailedEvent>(paymentEvent =>
                    paymentEvent.ConsumedBy.Contains("OrderService") &&
                    paymentEvent.ConsumedBy.Contains("NotificationService") &&
                    paymentEvent.ConsumedBy.Contains("QuoteEngine") &&
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.IdempotencyKey == "idem-failed" &&
                    paymentEvent.Payload.CustomerId == "customer-123" &&
                    paymentEvent.Payload.OrderId == "ORD-456" &&
                    paymentEvent.Payload.ProviderName == "stripe" &&
                    paymentEvent.Payload.ErrorMessage == "Card declined" &&
                    paymentEvent.Payload.ProviderErrorCode == "card_declined"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenThailandPrimaryProviderThrows_FallsBackToStripe()
    {
        var omise = CreateOmiseProvider();
        var stripe = CreateStripeProvider();
        var omiseAdapter = new FakePaymentProviderAdapter(
            "omise",
            new ProviderPaymentResult
            {
                Success = false,
                ProviderTransactionId = string.Empty,
                Status = "failed"
            },
            new HttpRequestException("Omise create charge timed out"));
        var stripeAdapter = new FakePaymentProviderAdapter(
            "stripe",
            new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = "cs_exception_fallback",
                Status = "open",
                PaymentUrl = "https://checkout.stripe.com/c/pay/cs_exception_fallback"
            });

        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(r => r.GetByIdempotencyKeyAsync("idem-exception-fallback", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.GetLatestCompletedByOrderIdAsync("order-789", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);
        repository
            .Setup(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction transaction, CancellationToken _) => transaction);
        repository
            .Setup(r => r.AddLogAsync(It.IsAny<TransactionLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var routing = new Mock<IPaymentRoutingService>();
        routing
            .SetupSequence(r => r.SelectProviderAsync("THB", "stripe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(omise)
            .ReturnsAsync(stripe);

        var idempotency = new Mock<IIdempotencyService>();
        idempotency
            .Setup(i => i.AcquireLockAsync("payment", "idem-exception-fallback", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var events = new Mock<IEventPublisher>();
        var metrics = new Mock<IMetricsService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var encryption = new Mock<IEncryptionService>();
        var providerFactory = new Mock<ProviderFactory>(httpClientFactory.Object, encryption.Object);
        providerFactory
            .Setup(f => f.CreateProvider(omise, null))
            .Returns(omiseAdapter);
        providerFactory
            .Setup(f => f.CreateProvider(stripe, null))
            .Returns(stripeAdapter);

        var service = new PaymentOrchestrationService(
            repository.Object,
            routing.Object,
            idempotency.Object,
            events.Object,
            metrics.Object,
            providerFactory.Object,
            new CircuitBreakerStateManager(),
            NullLogger<PaymentOrchestrationService>.Instance);

        var transaction = await service.ProcessPaymentAsync(new PaymentProcessingRequest
        {
            IdempotencyKey = "idem-exception-fallback",
            Amount = 2190m,
            Currency = "THB",
            CustomerId = "customer-456",
            OrderId = "order-789",
            Description = "Manufacturing order ORD-789",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-789",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-789",
            Metadata = new Dictionary<string, string> { ["orderNumber"] = "ORD-789" },
            PreferredProvider = "stripe",
            CorrelationId = Guid.NewGuid().ToString()
        });

        Assert.Equal("stripe", transaction.ProviderName);
        Assert.Equal(stripe.Id, transaction.PaymentProviderId);
        Assert.Equal("cs_exception_fallback", transaction.ProviderTransactionId);
        Assert.Equal(PaymentStatus.Processing, transaction.Status);
        Assert.Equal(1, omiseAdapter.ProcessPaymentCallCount);
        Assert.Equal(1, stripeAdapter.ProcessPaymentCallCount);
        events.Verify(
            e => e.PublishAsync(It.IsAny<PaymentFailedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        events.Verify(
            e => e.PublishAsync(
                It.Is<PaymentPendingEvent>(paymentEvent =>
                    paymentEvent.Payload.TransactionId == transaction.Id &&
                    paymentEvent.Payload.ProviderName == "stripe" &&
                    paymentEvent.Payload.ProviderEventCode == "ProviderSuccess"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PaymentProvider CreateStripeProvider()
    {
        var providerId = Guid.NewGuid();
        return new PaymentProvider
        {
            Id = providerId,
            Name = "stripe",
            DisplayName = "Stripe",
            Status = ProviderStatus.Active,
            Priority = 1,
            SupportedCurrencies = ["THB"],
            Credentials = new Dictionary<string, string> { ["ApiKey"] = "sk_test_123" },
            Configurations =
            [
                new ProviderConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentProviderId = providerId,
                    Region = "default",
                    ApiBaseUrl = "https://api.stripe.com",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static PaymentProvider CreateOmiseProvider()
    {
        var providerId = Guid.NewGuid();
        return new PaymentProvider
        {
            Id = providerId,
            Name = "omise",
            DisplayName = "Omise",
            Status = ProviderStatus.Active,
            Priority = 1,
            SupportedCurrencies = ["THB"],
            Credentials = new Dictionary<string, string> { ["SecretKey"] = "skey_test_123" },
            Configurations =
            [
                new ProviderConfiguration
                {
                    Id = Guid.NewGuid(),
                    PaymentProviderId = providerId,
                    Region = "default",
                    ApiBaseUrl = "https://api.omise.co",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static Dictionary<string, string> ReadForm(string? body)
    {
        Assert.False(string.IsNullOrWhiteSpace(body));
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => WebUtility.UrlDecode(parts[0]),
                parts => parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : string.Empty);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class FakePaymentProviderAdapter(
        string providerName,
        ProviderPaymentResult paymentResult,
        Exception? processException = null) : IPaymentProviderAdapter
    {
        public string ProviderName { get; } = providerName;

        public int ProcessPaymentCallCount { get; private set; }

        public Task<ProviderPaymentResult> ProcessPaymentAsync(
            ProviderPaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            ProcessPaymentCallCount++;
            if (processException is not null)
            {
                throw processException;
            }

            return Task.FromResult(paymentResult);
        }

        public Task<ProviderPaymentStatus> GetPaymentStatusAsync(
            string providerTransactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderPaymentStatus { Status = paymentResult.Status });
        }

        public Task<ProviderRefundResult> ProcessRefundAsync(
            ProviderRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderRefundResult
            {
                Success = false,
                ProviderRefundId = string.Empty,
                Status = "unsupported"
            });
        }

        public bool ValidateWebhookSignature(string payload, string signature, string secret) => true;
    }
}
