using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Providers;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Providers;

public sealed class ProviderFactoryTests
{
    [Fact]
    public async Task CreateProvider_OpnAlias_UsesOmiseAdapter()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient("opn"))
            .Returns(new HttpClient());
        var encryption = new Mock<IEncryptionService>();
        encryption.Setup(service => service.Decrypt("skey_test_opn")).Returns("skey_test_opn");
        var factory = new ProviderFactory(httpClientFactory.Object, encryption.Object);
        var provider = new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = "opn",
            DisplayName = "Opn Payments",
            Status = ProviderStatus.Active,
            Priority = 1,
            SupportedCurrencies = new List<string> { "THB" },
            Credentials = new Dictionary<string, string>
            {
                ["SecretKey"] = "skey_test_opn"
            },
            Configurations = new List<ProviderConfiguration>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PaymentProviderId = Guid.NewGuid(),
                    Region = "thailand",
                    ApiBaseUrl = "https://api.omise.co",
                    IsActive = true,
                    MaxRetries = 3,
                    TimeoutSeconds = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var adapter = factory.CreateProvider(provider);
        var result = await adapter.ProcessPaymentAsync(new ProviderPaymentRequest
        {
            IdempotencyKey = "opn-idem-1",
            Amount = 2500m,
            Currency = "THB",
            CustomerId = "customer-1",
            OrderId = "order-1",
            Description = "Manufacturing order ORD-1",
            ReturnUrl = "https://quote.example.com/payment/success",
            CancelUrl = "https://quote.example.com/payment/cancel",
            Metadata = new Dictionary<string, string>()
        });

        Assert.Equal("omise", adapter.ProviderName);
        Assert.True(result.Success);
        Assert.StartsWith("chrg_omise_", result.ProviderTransactionId, StringComparison.Ordinal);
        Assert.StartsWith("https://pay.omise.co/", result.PaymentUrl, StringComparison.Ordinal);
    }
}
