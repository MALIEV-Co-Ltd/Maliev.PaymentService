using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

/// <summary>
/// Unit tests for RefundService with validation logic and refund processing.
/// </summary>
public class RefundServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IRefundRepository> _refundRepositoryMock;
    private readonly Mock<IProviderRepository> _providerRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IMetricsService> _metricsServiceMock;
    private readonly Mock<ILogger<RefundService>> _loggerMock;
    private readonly RefundService _service;

    public RefundServiceTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _refundRepositoryMock = new Mock<IRefundRepository>();
        _providerRepositoryMock = new Mock<IProviderRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _metricsServiceMock = new Mock<IMetricsService>();
        _loggerMock = new Mock<ILogger<RefundService>>();

        _service = new RefundService(
            _paymentRepositoryMock.Object,
            _refundRepositoryMock.Object,
            _providerRepositoryMock.Object,
            _eventPublisherMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessRefundAsync_WhenPaymentNotFound_ShouldThrowException()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var amount = 100.00m;
        var reason = "Customer request";

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessRefundAsync(transactionId, amount, reason, "full"));
    }

    [Fact]
    public async Task ProcessRefundAsync_WhenPaymentNotCompleted_ShouldThrowException()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Pending, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessRefundAsync(transactionId, 50.00m, "test", "partial"));

        Assert.Contains("not completed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessRefundAsync_WhenRefundAmountExceedsRemaining_ShouldThrowException()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _refundRepositoryMock
            .Setup(r => r.GetByPaymentTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundTransaction>
            {
                CreateTestRefund(payment.Id, 60.00m, RefundStatus.Completed)
            });

        // Act & Assert - try to refund 50 when only 40 remaining
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessRefundAsync(transactionId, 50.00m, "test", "partial"));

        Assert.Contains("exceeds", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessRefundAsync_FullRefund_ShouldRefundEntireAmount()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _refundRepositoryMock
            .Setup(r => r.GetByPaymentTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundTransaction>());

        _refundRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<RefundTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundTransaction r, CancellationToken ct) => r);

        // Act
        var result = await _service.ProcessRefundAsync(transactionId, 100.00m, "Full refund", "full");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100.00m, result.Amount);
        Assert.Equal("full", result.RefundType);

        _refundRepositoryMock.Verify(
            r => r.AddAsync(It.Is<RefundTransaction>(rf => rf.Amount == 100.00m), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_PartialRefund_ShouldRefundSpecifiedAmount()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        _refundRepositoryMock
            .Setup(r => r.GetByPaymentTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundTransaction>());

        _refundRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<RefundTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundTransaction r, CancellationToken ct) => r);

        // Act
        var result = await _service.ProcessRefundAsync(transactionId, 50.00m, "Partial refund", "partial");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50.00m, result.Amount);
        Assert.Equal("partial", result.RefundType);

        _refundRepositoryMock.Verify(
            r => r.AddAsync(It.Is<RefundTransaction>(rf => rf.Amount == 50.00m), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_MultiplePartialRefunds_ShouldCalculateRemainingCorrectly()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Already refunded 30 + 20 = 50, so 50 remaining
        _refundRepositoryMock
            .Setup(r => r.GetByPaymentTransactionIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefundTransaction>
            {
                CreateTestRefund(payment.Id, 30.00m, RefundStatus.Completed),
                CreateTestRefund(payment.Id, 20.00m, RefundStatus.Completed)
            });

        _refundRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<RefundTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundTransaction r, CancellationToken ct) => r);

        // Act - request 50 refund (should succeed, exactly remaining amount)
        var result = await _service.ProcessRefundAsync(transactionId, 50.00m, "Final refund", "partial");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50.00m, result.Amount);
    }

    [Fact]
    public async Task ProcessRefundAsync_WhenRefundAmountZero_ShouldThrowException()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ProcessRefundAsync(transactionId, 0m, "test", "full"));
    }

    [Fact]
    public async Task ProcessRefundAsync_WhenRefundAmountNegative_ShouldThrowException()
    {
        // Arrange
        var transactionId = Guid.NewGuid();
        var payment = CreateTestPayment(transactionId, PaymentStatus.Completed, 100.00m);

        _paymentRepositoryMock
            .Setup(r => r.GetByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ProcessRefundAsync(transactionId, -10m, "test", "full"));
    }

    private static PaymentTransaction CreateTestPayment(Guid id, PaymentStatus status, decimal amount)
    {
        return new PaymentTransaction
        {
            Id = id,
            IdempotencyKey = $"pay-{id}",
            Amount = amount,
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
            CompletedAt = status == PaymentStatus.Completed ? DateTime.UtcNow : null,
            RowVersion = Array.Empty<byte>()
        };
    }

    private static RefundTransaction CreateTestRefund(Guid paymentId, decimal amount, RefundStatus status)
    {
        return new RefundTransaction
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = paymentId,
            ProviderId = Guid.NewGuid(),
            Amount = amount,
            Currency = "THB",
            Status = status,
            RefundType = "partial",
            IdempotencyKey = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };
    }
}
