using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Infrastructure.Encryption;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Factory for creating payment provider adapter instances.
/// Instantiates the correct provider implementation based on provider configuration.
/// </summary>
public class ProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEncryptionService _encryptionService;

    public ProviderFactory(IHttpClientFactory httpClientFactory, IEncryptionService encryptionService)
    {
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Creates a payment provider adapter instance.
    /// </summary>
    /// <param name="provider">Payment provider configuration</param>
    /// <param name="region">Optional region (defaults to first active configuration)</param>
    /// <returns>Provider adapter instance</returns>
    /// <exception cref="NotSupportedException">Thrown when provider type is not supported</exception>
    public IPaymentProviderAdapter CreateProvider(PaymentProvider provider, string? region = null)
    {
        var httpClient = _httpClientFactory.CreateClient(provider.Name);

        // Get the appropriate configuration for the region
        var config = region != null
            ? provider.Configurations.FirstOrDefault(c => c.Region == region && c.IsActive)
            : provider.Configurations.FirstOrDefault(c => c.IsActive);

        if (config == null)
        {
            throw new InvalidOperationException($"No active configuration found for provider {provider.Name} in region {region ?? "default"}");
        }

        // Decrypt credentials
        var decryptedCredentials = new Dictionary<string, string>();
        foreach (var (key, encryptedValue) in provider.Credentials)
        {
            decryptedCredentials[key] = _encryptionService.Decrypt(encryptedValue);
        }

        // Create provider-specific adapter
        return provider.Name.ToLowerInvariant() switch
        {
            "stripe" => new StripeProvider(
                httpClient,
                decryptedCredentials.GetValueOrDefault("ApiKey", string.Empty),
                config.ApiBaseUrl),

            "paypal" => new PayPalProvider(
                httpClient,
                decryptedCredentials.GetValueOrDefault("ClientId", string.Empty),
                decryptedCredentials.GetValueOrDefault("ClientSecret", string.Empty),
                config.ApiBaseUrl),

            "omise" => new OmiseProvider(
                httpClient,
                decryptedCredentials.GetValueOrDefault("SecretKey", string.Empty),
                config.ApiBaseUrl),

            "scb" => new ScbApiProvider(
                httpClient,
                decryptedCredentials.GetValueOrDefault("ApiKey", string.Empty),
                decryptedCredentials.GetValueOrDefault("ApiSecret", string.Empty),
                config.ApiBaseUrl),

            _ => throw new NotSupportedException($"Payment provider '{provider.Name}' is not supported")
        };
    }
}
