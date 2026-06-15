using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.PaymentService.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for Payments API endpoints.
/// Tests payment processing with real database and infrastructure.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class PaymentsControllerIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;
    private PaymentDbContext? _dbContext;

    private readonly ITestOutputHelper _output;

    public PaymentsControllerIntegrationTests(IntegrationTestWebAppFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = _factory.CreateClient();

        // Set JWT authorization header with administrative permissions and unique user ID
        var token = _factory.CreateTestJwtToken(
            userId: "payments-integration-test-admin",
            permissions: new[] { "payment.*" });
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        // Clean database before seeding
        await _factory.CleanDatabaseAsync();

        // Get DbContext
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<Maliev.PaymentService.Application.Interfaces.IEncryptionService>();

        // Seed test provider
        await SeedTestProviderAsync(encryptionService);
    }

    private async Task SeedTestProviderAsync(Maliev.PaymentService.Application.Interfaces.IEncryptionService encryptionService)
    {
        // Check if provider already exists (for idempotency across multiple tests)
        var existingProvider = await _dbContext!.PaymentProviders
            .FirstOrDefaultAsync(p => p.Name == "omise");

        if (existingProvider != null)
        {
            return; // Provider already seeded
        }

        var providerId = Guid.NewGuid();
        var provider = new Maliev.PaymentService.Domain.Entities.PaymentProvider
        {
            Id = providerId,
            Name = "omise",
            DisplayName = "Omise (Test)",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "THB" },
            Priority = 1,
            // Encrypt credentials so they can be decrypted when processing payments
            Credentials = new Dictionary<string, string>
            {
                { "PublicKey", encryptionService.Encrypt("pkey_test_mock_key") },
                { "SecretKey", encryptionService.Encrypt("skey_test_mock_key") },
                { "WebhookSecret", encryptionService.Encrypt("whsec_test_mock_key") }
            },
            Configurations = new List<Maliev.PaymentService.Domain.Entities.ProviderConfiguration>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PaymentProviderId = providerId,
                    Region = "thailand",
                    ApiBaseUrl = "https://api.omise.co",
                    IsActive = true,
                    MaxRetries = 3,
                    TimeoutSeconds = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext!.PaymentProviders.Add(provider);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedStripeFallbackProviderAsync(Maliev.PaymentService.Application.Interfaces.IEncryptionService encryptionService)
    {
        var existingProvider = await _dbContext!.PaymentProviders
            .FirstOrDefaultAsync(p => p.Name == "stripe");

        if (existingProvider != null)
        {
            return;
        }

        var providerId = Guid.NewGuid();
        var provider = new Maliev.PaymentService.Domain.Entities.PaymentProvider
        {
            Id = providerId,
            Name = "stripe",
            DisplayName = "Stripe (Fallback Test)",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "THB", "USD" },
            Priority = 0,
            Credentials = new Dictionary<string, string>
            {
                { "ApiKey", encryptionService.Encrypt("sk_test_fallback_key") }
            },
            Configurations = new List<Maliev.PaymentService.Domain.Entities.ProviderConfiguration>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PaymentProviderId = providerId,
                    Region = "global",
                    ApiBaseUrl = "https://api.stripe.com",
                    IsActive = true,
                    MaxRetries = 3,
                    TimeoutSeconds = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentProviders.Add(provider);
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
    public async Task ProcessPayment_WithCorrectPermission_ReturnsCreated()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "THB",
            CustomerId = "cust_123456",
            OrderId = "order_789",
            Description = "Test payment",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel",
            Metadata = new Dictionary<string, string>
            {
                { "booking_id", "booking_123" }
            }
        };

        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/payments", request);

        // Assert
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var body = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Response Body: {body}");
        }
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.TransactionId);
        Assert.Equal(100.00m, payment.Amount);
        Assert.Equal("THB", payment.Currency);
        Assert.Equal("cust_123456", payment.CustomerId);
        Assert.Contains(payment.Status, new[] { PaymentStatus.Pending, PaymentStatus.Processing, PaymentStatus.Completed });
        Assert.NotNull(payment.ProviderTransactionId);
        Assert.NotNull(payment.SelectedProvider);
    }

    [Fact]
    public async Task ProcessPayment_ThailandCurrencyWithStripePreference_SelectsOmisePrimaryProvider()
    {
        var encryptionService = _factory.Services.GetRequiredService<Maliev.PaymentService.Application.Interfaces.IEncryptionService>();
        await SeedStripeFallbackProviderAsync(encryptionService);

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "THB",
            CustomerId = "cust_th_primary",
            OrderId = "order_th_primary",
            Description = "Thailand primary provider test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel",
            PreferredProvider = "stripe"
        };

        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response = await _client.PostAsJsonAsync("/payment/v1/payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal("omise", payment.SelectedProvider);
        Assert.StartsWith("chrg_test_", payment.ProviderTransactionId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessPayment_WithDuplicateIdempotencyKey_ReturnsSameTransaction()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest
        {
            Amount = 50.00m,
            Currency = "THB",
            CustomerId = "cust_duplicate",
            OrderId = "order_dup_001",
            Description = "Duplicate test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act - First request
        var response1 = await _client.PostAsJsonAsync("/payment/v1/payments", request);
        var payment1 = await response1.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act - Second request with same idempotency key using fresh client
        using var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        client2.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client2.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response2 = await client2.PostAsJsonAsync("/payment/v1/payments", request);
        var payment2 = await response2.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.NotNull(payment1);
        Assert.NotNull(payment2);
        Assert.Equal(payment1.TransactionId, payment2.TransactionId);
        Assert.Equal(payment1.Amount, payment2.Amount);
        Assert.Equal(payment1.ProviderTransactionId, payment2.ProviderTransactionId);
    }

    [Fact]
    public async Task ProcessPayment_WithMissingIdempotencyKey_ReturnsBadRequest()
    {
        // Arrange
        var request = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "THB",
            CustomerId = "cust_123",
            OrderId = "order_001",
            Description = "Test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        // Intentionally NOT adding Idempotency-Key

        // Act
        var response = await _client.PostAsJsonAsync("/payment/v1/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessRefund_MissingIdempotencyKey_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync($"/payment/v1/payments/{Guid.NewGuid()}/refund", new { amount = 10, refundType = "partial" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Idempotency-Key", body);
    }

    private async Task UpdatePaymentStatusToCompleted(Guid transactionId)

    {
        // Helper method to simulate payment completion
        var payment = await _dbContext!.PaymentTransactions.FindAsync(transactionId);
        if (payment != null)
        {
            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }
}
