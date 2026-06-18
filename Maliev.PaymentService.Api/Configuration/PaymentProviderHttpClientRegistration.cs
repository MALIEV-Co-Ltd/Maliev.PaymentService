using Microsoft.Extensions.Http;

namespace Maliev.PaymentService.Api.Configuration;

/// <summary>
/// Registers named HTTP clients used by payment provider adapters.
/// </summary>
public static class PaymentProviderHttpClientRegistration
{
    /// <summary>
    /// Provider client names requested by ProviderFactory.
    /// </summary>
    public static readonly string[] ProviderClientNames = ["omise", "opn", "stripe", "scb"];

    /// <summary>
    /// Adds resilient HTTP clients for each supported payment provider adapter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPaymentProviderHttpClients(this IServiceCollection services)
    {
        foreach (var providerClientName in ProviderClientNames)
        {
            services.AddHttpClient(providerClientName)
                .AddStandardResilienceHandler();
        }

        return services;
    }
}
