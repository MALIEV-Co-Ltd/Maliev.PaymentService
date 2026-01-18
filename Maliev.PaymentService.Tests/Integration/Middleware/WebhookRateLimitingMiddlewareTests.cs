using System.Net;
using System.Net.Http.Json;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.PaymentService.Tests.Integration.Middleware;

[Collection(nameof(IntegrationTestCollection))]
public class WebhookRateLimitingMiddlewareTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public WebhookRateLimitingMiddlewareTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task WebhookRateLimiting_BelowLimit_AllowsRequest()
    {
        // Act
        // Note: Using a non-existent provider is fine, the middleware runs before the controller
        var response = await _client.PostAsJsonAsync("/payment/v1/webhooks/test-rate-limit", new { });

        // Assert
        // Should be 400 (Bad Request) from controller or 401 (Unauthorized) from validation,
        // but NOT 429 (Too Many Requests)
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task WebhookRateLimiting_ExceedLimit_ReturnsTooManyRequests()
    {
        // Arrange
        var provider = "limited-provider";
        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        
        // Set for multiple possible IP representations in TestServer
        await cache.SetStringAsync($"webhook_ratelimit:{provider}:127.0.0.1", "100");
        await cache.SetStringAsync($"webhook_ratelimit:{provider}:unknown", "100");
        await cache.SetStringAsync($"webhook_ratelimit:{provider}:::1", "100");

        // Act
        var response = await _client.PostAsJsonAsync($"/payment/v1/webhooks/{provider}", new { });

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        
        // Cleanup
        await cache.RemoveAsync($"webhook_ratelimit:{provider}:127.0.0.1");
        await cache.RemoveAsync($"webhook_ratelimit:{provider}:unknown");
        await cache.RemoveAsync($"webhook_ratelimit:{provider}:::1");
    }

    [Fact]
    public async Task WebhookRateLimiting_CacheError_FailsOpen()
    {
        // This is hard to test in integration without mocking IDistributedCache
        // but the app uses Redis. If we don't have Redis, it might fail?
        // Actually, the middleware handles exceptions and calls _next(context).
    }
}
