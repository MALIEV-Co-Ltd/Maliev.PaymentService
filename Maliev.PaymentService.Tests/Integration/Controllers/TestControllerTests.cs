using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Application.Authorization;
using Maliev.PaymentService.Tests.Fixtures;
using Xunit;

namespace Maliev.PaymentService.Tests.Integration.Controllers;

[Collection(nameof(IntegrationTestCollection))]
public class TestControllerTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public TestControllerTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PublishPaymentCompleted_ReturnsOk()
    {
        // Arrange
        var token = _factory.CreateTestJwtToken(
            userId: "payment-test-publisher",
            permissions: [PaymentPermissions.PaymentsProcess]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new PublishPaymentRequest
        {
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 100.0,
            Currency = "USD"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/test/publish-payment-completed", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublishPaymentCompleted_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new PublishPaymentRequest
        {
            OrderId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            Amount = 100.0,
            Currency = "USD"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/test/publish-payment-completed", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
