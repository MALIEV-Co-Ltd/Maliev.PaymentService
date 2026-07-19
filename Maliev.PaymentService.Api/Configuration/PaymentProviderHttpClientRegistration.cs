using Microsoft.Extensions.Http;
using System.Net;
using System.Net.Http.Json;

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
    /// <param name="environmentName">Current host environment name.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPaymentProviderHttpClients(this IServiceCollection services, string? environmentName = null)
    {
        foreach (var providerClientName in ProviderClientNames)
        {
            var clientBuilder = services.AddHttpClient(providerClientName);

            if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            {
                clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new TestingPaymentProviderHandler(providerClientName));
            }

            clientBuilder
                .AddStandardResilienceHandler();
        }

        return services;
    }

    private sealed class TestingPaymentProviderHandler(string providerName) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var normalizedProvider = providerName.ToLowerInvariant();
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if ((normalizedProvider is "omise" or "opn") &&
                request.Method == HttpMethod.Post &&
                path.Equals("/charges", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = $"chrg_test_{Guid.NewGuid():N}",
                        status = "pending",
                        authorize_uri = "https://pay.omise.co/payments/paym_test_make_studio"
                    })
                });
            }

            if (normalizedProvider == "stripe" &&
                request.Method == HttpMethod.Post &&
                path.Equals("/v1/checkout/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = $"cs_test_{Guid.NewGuid():N}",
                        url = "https://checkout.stripe.com/c/pay/cs_test_make_studio",
                        status = "open"
                    })
                });
            }

            if (normalizedProvider == "scb" && request.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = $"scb_test_{Guid.NewGuid():N}",
                        status = "PENDING",
                        paymentUrl = "https://pay.scb.co.th/qr/scb_test_make_studio"
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new
                {
                    error = "Unsupported testing payment provider request"
                })
            });
        }
    }
}
