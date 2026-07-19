using Maliev.PaymentService.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Configuration;

public sealed class PaymentProviderHttpClientRegistrationTests
{
    [Theory]
    [InlineData("omise")]
    [InlineData("opn")]
    [InlineData("stripe")]
    [InlineData("scb")]
    public void AddPaymentProviderHttpClients_ConfiguresResilientClientForProviderFactoryName(string providerClientName)
    {
        var services = new ServiceCollection();

        services.AddPaymentProviderHttpClients();

        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
        var options = optionsMonitor.Get(providerClientName);

        Assert.NotEmpty(options.HttpMessageHandlerBuilderActions);
    }

    [Fact]
    public async Task AddPaymentProviderHttpClients_TestingEnvironment_UsesDeterministicOmiseCheckout()
    {
        var services = new ServiceCollection();

        services.AddPaymentProviderHttpClients("Testing");

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("omise");
        using var response = await client.PostAsync(
            "https://api.omise.co/charges",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["amount"] = "10000",
                ["currency"] = "thb",
                ["return_uri"] = "https://quote.test/payment/success"
            }));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("chrg_test_", body, StringComparison.Ordinal);
        Assert.Contains("https://pay.omise.co/payments/paym_test_make_studio", body, StringComparison.Ordinal);
    }
}
