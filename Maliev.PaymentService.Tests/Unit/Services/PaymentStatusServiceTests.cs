using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

/// <summary>
/// Unit tests for PaymentStatusService with Redis caching behavior.
/// </summary>
public class PaymentStatusServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<IMetricsService> _metricsServiceMock;
    private readonly Mock<ILogger<PaymentStatusService>> _loggerMock;
    private readonly PaymentStatusService _service;

    public PaymentStatusServiceTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _cacheMock = new Mock<IDistributedCache>();
        _metricsServiceMock = new Mock<IMetricsService>();
        _loggerMock = new Mock<ILogger<PaymentStatusService>>();
        _service = new PaymentStatusService(
            _paymentRepositoryMock.Object,
            _cacheMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenCacheHit_ShouldReturnCachedData()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var cachedTransaction = CreateTestTransaction(transactionId, PaymentStatus.Pending);

        var cachedJson = JsonSerializer.SerializeToUtf8Bytes(cachedTransaction);
        _cacheMock
            .Setup(c => c.GetAsync($"payment_status:{transactionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        // Act
        var result = await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transactionId, result.Id);
        Assert.Equal(PaymentStatus.Pending, result.Status);

        _paymentRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not query database when cache hit");

        _metricsServiceMock.Verify(
            m => m.RecordPaymentStatusCacheHit(It.IsAny<string>()),
            Times.Once,
            "Should record cache hit");
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenCacheMiss_ShouldQueryDatabaseAndCache()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var transaction = CreateTestTransaction(transactionId, PaymentStatus.Pending);

        _cacheMock
            .Setup(c => c.GetAsync($"payment_status:{transactionId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Cache miss

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transactionId, result.Id);
        Assert.Equal(PaymentStatus.Pending, result.Status);

        _paymentRepositoryMock.Verify(
            r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should query database on cache miss");

        _cacheMock.Verify(
            c => c.SetAsync(
                $"payment_status:{transactionId}",
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(60)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Should cache result with 60 second TTL");
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenActiveTransaction_ShouldCacheFor60Seconds()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var transaction = CreateTestTransaction(transactionId, PaymentStatus.Pending); // Active status

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        _cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(60)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenCompletedTransaction_ShouldCacheFor3600Seconds()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var transaction = CreateTestTransaction(transactionId, PaymentStatus.Completed); // Terminal status

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        _cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(3600)),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Completed transactions should cache for 1 hour");
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenTransactionNotFound_ShouldReturnNull()
    {
        // Arrange
        var transactionId = Guid.NewGuid();

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        // Act
        var result = await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        Assert.Null(result);

        _cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Should not cache null results");
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenCacheThrowsException_ShouldFallbackToDatabase()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var transaction = CreateTestTransaction(transactionId, PaymentStatus.Completed);

        _cacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis connection error"));

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _service.GetPaymentStatusAsync(transactionId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(transactionId, result.Id);
        _paymentRepositoryMock.Verify(
            r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should fallback to database when cache fails");
    }

    private static PaymentTransaction CreateTestTransaction(Guid id, PaymentStatus status)
    {
        return new PaymentTransaction
        {
            Id = id,
            IdempotencyKey = $"test-key-{id}",
            Amount = 100.00m,
            Currency = "THB",
            Status = status,
            CustomerId = "cust_test",
            OrderId = "order_test",
            Description = "Test payment",
            PaymentProviderId = Guid.NewGuid(),
            ProviderName = "stripe",
            ProviderTransactionId = $"txn_{id}",
            ReturnUrl = "https://example.com/return",
            CancelUrl = "https://example.com/cancel",
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };
    }
}
