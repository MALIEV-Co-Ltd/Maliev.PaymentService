using System.Net;
using System.Net.Http.Json;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maliev.PaymentService.Tests.Fixtures;

public class IntegrationTestWebAppFactory : BaseIntegrationTestFactory<Program, PaymentDbContext>
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory, TestPaymentProviderHttpClientFactory>();
    }

    private sealed class TestPaymentProviderHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "omise" or "opn" => new HttpClient(new OmiseHandler()),
                _ => new HttpClient(new EmptyHandler())
            };
        }
    }

    private sealed class OmiseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/charges")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = $"chrg_test_{Guid.NewGuid():N}",
                        status = "pending",
                        authorize_uri = "https://pay.omise.co/payments/paym_test_integration"
                    })
                });
            }

            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.StartsWith("/charges/", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        id = request.RequestUri.AbsolutePath.Split('/').Last(),
                        status = "successful"
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { error = "Unsupported Omise test request" })
            });
        }
    }

    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { error = "Unsupported provider test request" })
            });
        }
    }
}
