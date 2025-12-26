using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Authorization;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.PaymentService.Tests.Integration.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public class AuthorizationBenchmarkTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public AuthorizationBenchmarkTests(IntegrationTestWebAppFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AuthorizationLatency_ShouldBeWithinLimits()
    {
        // Arrange
        var token = _factory.CreateTestJwtToken(permissions: new[] { PaymentPermissions.PaymentsRead });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var path = "payment/v1/payments/" + Guid.NewGuid();

        // Warm up
        await _client.GetAsync(path);

        // Measure
        var sw = Stopwatch.StartNew();
        var iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            await _client.GetAsync(path);
        }
        sw.Stop();

        var averageLatency = sw.ElapsedMilliseconds / (double)iterations;
        _output.WriteLine($"Average authorization latency: {averageLatency:F2}ms");

        // Assert (SC-003: < 30ms)
        Assert.True(averageLatency < 30, $"Average latency {averageLatency}ms exceeded 30ms limit");
    }

    [Fact]
    public async Task RevocationSpeed_ShouldBeWithinLimits()
    {
        // Arrange
        var userId = "benchmark-revoked-user";
        var permission = PaymentPermissions.PaymentsProcess;
        var token = _factory.CreateTestJwtToken(userId: userId, permissions: new[] { permission });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var cache = _factory.Services.GetRequiredService<IDistributedCache>();
        var revocationKey = $"revoked:user:{userId}:permission:{permission}";

        // Act & Measure
        var sw = Stopwatch.StartNew();
        
        // 1. Revoke
        await cache.SetStringAsync(revocationKey, "true");
        
        // 2. Attempt access
        var response = await _client.PostAsJsonAsync("payment/v1/payments", new { });
        
        sw.Stop();

        _output.WriteLine($"Revocation enforcement took: {sw.ElapsedMilliseconds}ms");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // SC-004: < 5 seconds (5000ms)
        Assert.True(sw.ElapsedMilliseconds < 5000, "Revocation enforcement took too long");

        // Cleanup
        await cache.RemoveAsync(revocationKey);
    }
}
