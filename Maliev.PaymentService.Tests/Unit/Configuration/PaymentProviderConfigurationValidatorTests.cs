using Maliev.PaymentService.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace Maliev.PaymentService.Tests.Unit.Configuration;

public sealed class PaymentProviderConfigurationValidatorTests
{
    [Fact]
    public void ValidateOmiseForEnvironment_ProductionMissingSecrets_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Production"));

        Assert.Contains("PaymentProviders:Omise:PublicKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:SecretKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:WebhookSecret", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:ApiBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOmiseForEnvironment_ProductionDevelopmentPlaceholders_Throws()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PaymentProviders:Omise:PublicKey"] = "local-placeholder-omise-public-key",
            ["PaymentProviders:Omise:SecretKey"] = "local-placeholder-omise-secret-key",
            ["PaymentProviders:Omise:WebhookSecret"] = "local-placeholder-omise-webhook-secret",
            ["PaymentProviders:Omise:ApiBaseUrl"] = "https://api.omise.co"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Production"));

        Assert.Contains("development placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PaymentProviders:Omise:PublicKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:SecretKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:WebhookSecret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOmiseForEnvironment_TestingMissingSecrets_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().Build();

        PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Testing");
    }

    [Fact]
    public void ValidateOmiseForEnvironment_DevelopmentMissingSecrets_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().Build();

        PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Development");
    }

    [Fact]
    public void ValidateOmiseForEnvironment_StagingMissingSecrets_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Staging"));

        Assert.Contains("PaymentProviders:Omise:PublicKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:SecretKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:WebhookSecret", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Omise:ApiBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOmiseForEnvironment_ProductionConfigured_DoesNotThrow()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PaymentProviders:Omise:PublicKey"] = "configured-omise-public-key",
            ["PaymentProviders:Omise:SecretKey"] = "configured-omise-secret-key",
            ["PaymentProviders:Omise:WebhookSecret"] = "configured-omise-webhook-secret",
            ["PaymentProviders:Omise:ApiBaseUrl"] = "https://api.omise.co"
        });

        PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Production");
    }

    [Fact]
    public void ValidateStripeForEnvironment_ProductionMissingSecrets_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Production"));

        Assert.Contains("PaymentProviders:Stripe:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Stripe:WebhookSecret", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Stripe:ApiBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStripeForEnvironment_ProductionDevelopmentPlaceholders_Throws()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PaymentProviders:Stripe:ApiKey"] = "local-placeholder-stripe-api-key",
            ["PaymentProviders:Stripe:WebhookSecret"] = "local-placeholder-stripe-webhook-secret",
            ["PaymentProviders:Stripe:ApiBaseUrl"] = "https://api.stripe.com"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Production"));

        Assert.Contains("development placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PaymentProviders:Stripe:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Stripe:WebhookSecret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStripeForEnvironment_TestingMissingSecrets_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().Build();

        PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Testing");
    }

    [Fact]
    public void ValidateStripeForEnvironment_DevelopmentMissingSecrets_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().Build();

        PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Development");
    }

    [Fact]
    public void ValidateStripeForEnvironment_StagingMissingSecrets_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Staging"));

        Assert.Contains("PaymentProviders:Stripe:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Stripe:WebhookSecret", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PaymentProviders:Stripe:ApiBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStripeForEnvironment_ProductionConfiguredTestMode_DoesNotThrow()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PaymentProviders:Stripe:ApiKey"] = "configured-stripe-api-key",
            ["PaymentProviders:Stripe:WebhookSecret"] = "configured-stripe-webhook-secret",
            ["PaymentProviders:Stripe:ApiBaseUrl"] = "https://api.stripe.com"
        });

        PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(configuration, "Production");
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
