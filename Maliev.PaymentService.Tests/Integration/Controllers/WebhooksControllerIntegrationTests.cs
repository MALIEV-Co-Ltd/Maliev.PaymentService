using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.PaymentService.Tests.Integration.Controllers;

[Collection(nameof(IntegrationTestCollection))]
public class WebhooksControllerIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;
    private PaymentDbContext? _dbContext;

    public WebhooksControllerIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();

        var token = _factory.CreateTestJwtToken(
            userId: "webhooks-test-admin",
            permissions: new[] { "payment.*" });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await SeedTestProviderAsync();
    }

    private async Task SeedTestProviderAsync()
    {
        var provider = new Maliev.PaymentService.Domain.Entities.PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = "stripe",
            DisplayName = "Stripe",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 1,
            Credentials = new Dictionary<string, string>
            {
                { "WebhookSecret", "whsec_test" }
            },
            Configurations = new List<Maliev.PaymentService.Domain.Entities.ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.PaymentProviders.Add(provider);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReceiveWebhook_UnknownProvider_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/unknown", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new { id = "evt_123", type = "payment_intent.succeeded" };
        _client.DefaultRequestHeaders.Remove("Stripe-Signature");
        _client.DefaultRequestHeaders.Add("Stripe-Signature", "invalid");

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/stripe", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestWebhook_WithValidPermission_ReturnsOk()
    {
        // Arrange
        var request = new TestWebhookRequest
        {
            EventType = "payment.succeeded",
            TransactionId = Guid.NewGuid(),
            CustomData = new Dictionary<string, object>
            {
                { "amount", 1000 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/stripe/test", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WebhookReceivedResponse>();
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task TestWebhook_UnknownProvider_ReturnsBadRequest()
    {
        // Arrange
        var request = new TestWebhookRequest { EventType = "test" };

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/nonexistent/test", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_ValidStripeSignature_ReturnsOk()
    {
        // Arrange
        var payload = new { id = "evt_test_123", type = "payment_intent.succeeded" };
        var rawPayload = JsonSerializer.Serialize(payload);
        var secret = "whsec_test";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{rawPayload}";

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var signature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        var headerValue = $"t={timestamp},v1={signature}";

        _client.DefaultRequestHeaders.Remove("Stripe-Signature");
        _client.DefaultRequestHeaders.Add("Stripe-Signature", headerValue);

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/stripe", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WebhookReceivedResponse>();
        Assert.NotNull(result);
        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingEventId_ReturnsBadRequest()
    {
        // Arrange
        // For Stripe, missing 'id'
        var payload = new { type = "payment_intent.succeeded" };
        var rawPayload = JsonSerializer.Serialize(payload);
        var secret = "whsec_test";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{rawPayload}";

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        var signature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var headerValue = $"t={timestamp},v1={signature}";

        _client.DefaultRequestHeaders.Remove("Stripe-Signature");
        _client.DefaultRequestHeaders.Add("Stripe-Signature", headerValue);

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/stripe", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
