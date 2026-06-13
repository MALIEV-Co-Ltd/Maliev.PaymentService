using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PaymentService.Tests.Integration.Startup;

[Collection(nameof(IntegrationTestCollection))]
public sealed class LocalPaymentProviderSeedingTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public LocalPaymentProviderSeedingTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TestingStartup_SeedsStripeTestProviderForCheckoutAndWebhooks()
    {
        using var client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var stripe = await dbContext.PaymentProviders
            .Include(provider => provider.Configurations)
            .SingleAsync(provider => provider.Name == "stripe");

        Assert.Equal(ProviderStatus.Active, stripe.Status);
        Assert.Equal(2, stripe.Priority);
        Assert.Contains("THB", stripe.SupportedCurrencies);
        Assert.Contains("USD", stripe.SupportedCurrencies);
        Assert.True(stripe.Credentials.ContainsKey("ApiKey"));
        Assert.True(stripe.Credentials.ContainsKey("WebhookSecret"));
        Assert.Equal("sk_test_development_stripe_key", encryptionService.Decrypt(stripe.Credentials["ApiKey"]));
        Assert.Equal("whsec_stripe_development_secret", encryptionService.Decrypt(stripe.Credentials["WebhookSecret"]));

        var configuration = Assert.Single(stripe.Configurations);
        Assert.Equal("global", configuration.Region);
        Assert.Equal("https://api.stripe.com", configuration.ApiBaseUrl);
        Assert.True(configuration.IsActive);
        Assert.Equal(3, configuration.MaxRetries);
        Assert.Equal(30, configuration.TimeoutSeconds);
    }
}
