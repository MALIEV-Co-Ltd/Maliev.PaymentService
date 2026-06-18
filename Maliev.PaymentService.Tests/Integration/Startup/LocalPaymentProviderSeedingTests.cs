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
    public async Task TestingStartup_SeedsOmisePrimaryAndStripeFallbackProvidersForCheckoutAndWebhooks()
    {
        using var client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var omise = await dbContext.PaymentProviders
            .Include(provider => provider.Configurations)
            .SingleAsync(provider => provider.Name == "omise");

        Assert.Equal(ProviderStatus.Active, omise.Status);
        Assert.Equal(1, omise.Priority);
        Assert.Contains("THB", omise.SupportedCurrencies);
        Assert.DoesNotContain("USD", omise.SupportedCurrencies);
        Assert.True(omise.Credentials.ContainsKey("PublicKey"));
        Assert.True(omise.Credentials.ContainsKey("SecretKey"));
        Assert.True(omise.Credentials.ContainsKey("WebhookSecret"));
        Assert.Equal("pkey_test_development_omise_key", encryptionService.Decrypt(omise.Credentials["PublicKey"]));
        Assert.Equal("skey_test_development_omise_key", encryptionService.Decrypt(omise.Credentials["SecretKey"]));
        Assert.Equal("whsec_omise_development_secret", encryptionService.Decrypt(omise.Credentials["WebhookSecret"]));

        var omiseConfiguration = Assert.Single(omise.Configurations);
        Assert.Equal("thailand", omiseConfiguration.Region);
        Assert.Equal("https://api.omise.co", omiseConfiguration.ApiBaseUrl);
        Assert.True(omiseConfiguration.IsActive);
        Assert.Equal(3, omiseConfiguration.MaxRetries);
        Assert.Equal(30, omiseConfiguration.TimeoutSeconds);

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

        var stripeConfiguration = Assert.Single(stripe.Configurations);
        Assert.Equal("global", stripeConfiguration.Region);
        Assert.Equal("https://api.stripe.com", stripeConfiguration.ApiBaseUrl);
        Assert.True(stripeConfiguration.IsActive);
        Assert.Equal(3, stripeConfiguration.MaxRetries);
        Assert.Equal(30, stripeConfiguration.TimeoutSeconds);
    }
}
