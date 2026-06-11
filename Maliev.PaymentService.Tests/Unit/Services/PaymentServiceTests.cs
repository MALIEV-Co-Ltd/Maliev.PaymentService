using System.Net;
using System.Net.Http.Json;
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
}
