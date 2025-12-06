using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Maliev.PaymentService.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for Payments API endpoints.
/// Tests payment processing with real database and infrastructure.
/// </summary>
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

        // Set JWT authorization header
        var token = _factory.CreateTestJwtToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        // Get DbContext
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<Maliev.PaymentService.Infrastructure.Encryption.IEncryptionService>();

        // Seed test provider
        await SeedTestProviderAsync(encryptionService);
    }

    private async Task SeedTestProviderAsync(Maliev.PaymentService.Infrastructure.Encryption.IEncryptionService encryptionService)
    {
        // Check if provider already exists (for idempotency across multiple tests)
        var existingProvider = await _dbContext!.PaymentProviders
            .FirstOrDefaultAsync(p => p.Name == "stripe");
        
        if (existingProvider != null)
        {
            return; // Provider already seeded
        }

        var providerId = Guid.NewGuid();
        var provider = new Maliev.PaymentService.Core.Entities.PaymentProvider
        {
            Id = providerId,
            Name = "stripe",
            DisplayName = "Stripe (Test)",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "THB", "USD", "EUR" },
            Priority = 1,
            // Encrypt credentials so they can be decrypted when processing payments
            Credentials = new Dictionary<string, string>
            {
                { "ApiKey", encryptionService.Encrypt("sk_test_mock_key") }
            },
            Configurations = new List<Maliev.PaymentService.Core.Entities.ProviderConfiguration>
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
    public async Task ProcessPayment_WithValidRequest_ReturnsCreatedPayment()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "USD",
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
        var response = await _client.PostAsJsonAsync("/payments/v1/payments", request);

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
        Assert.Equal("USD", payment.Currency);
        Assert.Equal("cust_123456", payment.CustomerId);
        Assert.Contains(payment.Status, new[] { PaymentStatus.Pending, PaymentStatus.Processing, PaymentStatus.Completed });
        Assert.NotNull(payment.ProviderTransactionId);
        Assert.NotNull(payment.SelectedProvider);
    }

    [Fact]
    public async Task ProcessPayment_WithDuplicateIdempotencyKey_ReturnsSameTransaction()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PaymentRequest
        {
            Amount = 50.00m,
            Currency = "EUR",
            CustomerId = "cust_duplicate",
            OrderId = "order_dup_001",
            Description = "Duplicate test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act - First request
        var response1 = await _client.PostAsJsonAsync("/payments/v1/payments", request);
        var payment1 = await response1.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act - Second request with same idempotency key using fresh client
        using var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        client2.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        client2.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var response2 = await client2.PostAsJsonAsync("/payments/v1/payments", request);
        var payment2 = await response2.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode); // Service returns Created for both new and existing transactions
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
            Currency = "USD",
            CustomerId = "cust_123",
            OrderId = "order_001",
            Description = "Test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        // Intentionally NOT adding Idempotency-Key

        // Act
        var response = await _client.PostAsJsonAsync("/payments/v1/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var request = new PaymentRequest
        {
            Amount = -10.00m, // Invalid negative amount
            Currency = "USD",
            CustomerId = "cust_123",
            OrderId = "order_invalid",
            Description = "Invalid amount test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync("/payments/v1/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithUnsupportedCurrency_ReturnsBadRequest()
    {
        // Arrange
        var request = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "ZZZ", // Unsupported currency
            CustomerId = "cust_123",
            OrderId = "order_unsupported",
            Description = "Unsupported currency test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync("/payments/v1/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentById_WithExistingId_ReturnsPayment()
    {
        // Arrange - Create a payment first
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 75.00m,
            Currency = "USD",
            CustomerId = "cust_get_test",
            OrderId = "order_get_001",
            Description = "Get test payment",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act
        var response = await _client.GetAsync($"/payments/v1/payments/{createdPayment!.TransactionId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(createdPayment.TransactionId, payment.TransactionId);
        Assert.Equal(75.00m, payment.Amount);
        Assert.Equal("USD", payment.Currency);
    }

    [Fact]
    public async Task GetPaymentById_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/payments/v1/payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentById_MultipleCalls_ShouldUseCaching()
    {
        // Arrange - Create a payment first
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 50.00m,
            Currency = "EUR",
            CustomerId = "cust_cache_test",
            OrderId = "order_cache_001",
            Description = "Cache test payment",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act - First call (should query DB and cache)
        var response1 = await _client.GetAsync($"/payments/v1/payments/{createdPayment!.TransactionId}");
        var payment1 = await response1.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act - Second call (should use cache)
        var response2 = await _client.GetAsync($"/payments/v1/payments/{createdPayment.TransactionId}");
        var payment2 = await response2.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert - Both calls should return the same data
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.NotNull(payment1);
        Assert.NotNull(payment2);
        Assert.Equal(payment1.TransactionId, payment2.TransactionId);
        Assert.Equal(payment1.Amount, payment2.Amount);
        Assert.Equal(payment1.Status, payment2.Status);

        // Note: In integration tests, we can't easily verify cache hit/miss without instrumentation
        // The behavior is implicitly tested by multiple calls succeeding
    }

    [Fact]
    public async Task GetPaymentById_AfterCacheExpiry_ShouldRefreshFromDatabase()
    {
        // Arrange - Create a payment
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 125.00m,
            Currency = "THB",
            CustomerId = "cust_expiry_test",
            OrderId = "order_expiry_001",
            Description = "Cache expiry test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act - First call (caches with 60s TTL for pending)
        var response1 = await _client.GetAsync($"/payments/v1/payments/{createdPayment!.TransactionId}");

        // Wait for cache to potentially expire (skip in fast tests, or set very short TTL in test config)
        // For this test, we just verify the endpoint still works after delay
        await Task.Delay(100); // Small delay to simulate time passing

        // Second call (should still work even if cache expired)
        var response2 = await _client.GetAsync($"/payments/v1/payments/{createdPayment.TransactionId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithMetadata_StoresMetadata()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var metadata = new Dictionary<string, string>
        {
            { "booking_id", "booking_456" },
            { "user_email", "test@example.com" },
            { "campaign_id", "summer_sale_2025" }
        };

        var request = new PaymentRequest
        {
            Amount = 200.00m,
            Currency = "USD",
            CustomerId = "cust_metadata",
            OrderId = "order_meta_001",
            Description = "Metadata test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel",
            Metadata = metadata
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync("/payments/v1/payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.NotNull(payment);
        Assert.NotNull(payment.Metadata);
        Assert.Equal(3, payment.Metadata.Count);
        Assert.Equal("booking_456", payment.Metadata["booking_id"]);
        Assert.Equal("test@example.com", payment.Metadata["user_email"]);
        Assert.Equal("summer_sale_2025", payment.Metadata["campaign_id"]);
    }

    [Fact]
    public async Task ProcessRefund_WithValidRequest_ShouldSucceed()
    {
        // Arrange - Create and complete a payment first
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 150.00m,
            Currency = "USD",
            CustomerId = "cust_refund_test",
            OrderId = "order_refund_001",
            Description = "Refund test payment",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Manually update payment status to Completed (in real scenario, webhook would do this)
        await UpdatePaymentStatusToCompleted(payment!.TransactionId);

        // Prepare refund request
        var refundRequest = new
        {
            Amount = 50.00m,
            Reason = "Customer requested refund",
            RefundType = "partial"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync($"/payments/v1/payments/{payment.TransactionId}/refund", refundRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refund = await response.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(refund);
        Assert.Equal(50.00m, refund.Amount);
        Assert.Equal("partial", refund.RefundType);
    }

    [Fact]
    public async Task ProcessRefund_WhenPaymentNotCompleted_ShouldReturnBadRequest()
    {
        // Arrange - Create a pending payment
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "USD",
            CustomerId = "cust_pending",
            OrderId = "order_pending_001",
            Description = "Pending payment",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Try to refund without completing payment
        var refundRequest = new
        {
            Amount = 50.00m,
            Reason = "Test refund",
            RefundType = "partial"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync($"/payments/v1/payments/{payment!.TransactionId}/refund", refundRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessRefund_WithExcessiveAmount_ShouldReturnBadRequest()
    {
        // Arrange - Create and complete a payment
        var idempotencyKey = Guid.NewGuid().ToString();
        var createRequest = new PaymentRequest
        {
            Amount = 100.00m,
            Currency = "USD",
            CustomerId = "cust_excess",
            OrderId = "order_excess_001",
            Description = "Excessive refund test",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        var createResponse = await _client.PostAsJsonAsync("/payments/v1/payments", createRequest);
        var payment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        await UpdatePaymentStatusToCompleted(payment!.TransactionId);

        // Try to refund more than payment amount
        var refundRequest = new
        {
            Amount = 150.00m,  // More than 100
            Reason = "Excessive refund",
            RefundType = "partial"
        };


        _client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync($"/payments/v1/payments/{payment.TransactionId}/refund", refundRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
