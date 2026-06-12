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
            ["PaymentProviders:Omise:PublicKey"] = "pkey_test_development_omise_key",
            ["PaymentProviders:Omise:SecretKey"] = "skey_test_development_omise_key",
            ["PaymentProviders:Omise:WebhookSecret"] = "whsec_omise_development_secret",
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
    public void ValidateOmiseForEnvironment_ProductionConfigured_DoesNotThrow()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PaymentProviders:Omise:PublicKey"] = "pkey_live_configured",
            ["PaymentProviders:Omise:SecretKey"] = "skey_live_configured",
            ["PaymentProviders:Omise:WebhookSecret"] = "whsec_live_configured",
            ["PaymentProviders:Omise:ApiBaseUrl"] = "https://api.omise.co"
        });

        PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(configuration, "Production");
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
