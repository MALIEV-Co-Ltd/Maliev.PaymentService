using Maliev.PaymentService.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
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
}
