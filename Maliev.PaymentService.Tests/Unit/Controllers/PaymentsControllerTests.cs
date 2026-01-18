using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Controllers;

public class PaymentsControllerTests
{
    private readonly Mock<IPaymentService> _paymentServiceMock = new();
    private readonly Mock<IRefundService> _refundServiceMock = new();
    private readonly Mock<IPaymentRoutingService> _routingServiceMock = new();
    private readonly Mock<IMetricsService> _metricsServiceMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<PaymentsController>> _loggerMock = new();
    private readonly PaymentsController _controller;

    public PaymentsControllerTests()
    {
        _controller = new PaymentsController(
            _paymentServiceMock.Object,
            _refundServiceMock.Object,
            _routingServiceMock.Object,
            _metricsServiceMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
            
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ProcessPayment_MissingIdempotencyKey_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ProcessPayment(new PaymentRequest { Amount = 10, Currency = "USD", CustomerId = "c", OrderId = "o", Description = "d", ReturnUrl = "https://r", CancelUrl = "https://c" }, default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ProcessPayment_InternalError_Returns500()
    {
        // Arrange
        _controller.Request.Headers["Idempotency-Key"] = "test-key";
        _routingServiceMock.Setup(x => x.SelectProviderAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.ProcessPayment(new PaymentRequest { Amount = 10, Currency = "USD", CustomerId = "c", OrderId = "o", Description = "d", ReturnUrl = "https://r", CancelUrl = "https://c" }, default);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_UnsupportedCurrency_ReturnsBadRequest()
    {
        // Arrange
        _controller.Request.Headers["Idempotency-Key"] = "test-key";
        _routingServiceMock.Setup(x => x.SelectProviderAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentProvider)null!);

        // Act
        var result = await _controller.ProcessPayment(new PaymentRequest { Amount = 10, Currency = "ZZZ", CustomerId = "c", OrderId = "o", Description = "d", ReturnUrl = "https://r", CancelUrl = "https://c" }, default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ProcessPayment_Idempotent_ReturnsOk()
    {
        // Arrange
        _controller.Request.Headers["Idempotency-Key"] = "test-key";
        var provider = CreateTestProvider("stripe");
        _routingServiceMock.Setup(x => x.SelectProviderAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
        
        var existingTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            Status = PaymentStatus.Completed,
            PaymentProvider = provider,
            IdempotencyKey = "test-key",
            Amount = 10,
            Currency = "USD",
            CustomerId = "c",
            OrderId = "o",
            Description = "d",
            PaymentProviderId = provider.Id,
            ProviderName = "stripe",
            ProviderTransactionId = "p1",
            ReturnUrl = "https://r",
            CancelUrl = "https://c",
            CorrelationId = "cor1",
            UpdatedAt = DateTime.UtcNow
        };
        _paymentServiceMock.Setup(x => x.ProcessPaymentAsync(It.IsAny<PaymentProcessingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTransaction);

        // Act
        var result = await _controller.ProcessPayment(new PaymentRequest { Amount = 10, Currency = "USD", CustomerId = "c", OrderId = "o", Description = "d", ReturnUrl = "https://r", CancelUrl = "https://c" }, default);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Processing)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.PartiallyRefunded)]
    [InlineData((PaymentStatus)999)] // Default case
    public async Task GetPaymentById_DifferentStatuses_CachesCorrectly(PaymentStatus status)
    {
        // Arrange
        var id = Guid.NewGuid();
        var provider = CreateTestProvider("stripe");
        var transaction = new PaymentTransaction
        {
            Id = id,
            Status = status,
            Amount = 10,
            Currency = "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "k",
            CustomerId = "c",
            OrderId = "o",
            Description = "d",
            PaymentProviderId = provider.Id,
            ProviderName = "stripe",
            ProviderTransactionId = "p",
            ReturnUrl = "r",
            CancelUrl = "c",
            CorrelationId = "cor",
            PaymentProvider = provider
        };
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(transaction);

        // Act
        var result = await _controller.GetPaymentById(id, default);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _cacheMock.Verify(x => x.SetAsync(
            $"payment:{id}",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefund_InvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        _controller.Request.Headers["Idempotency-Key"] = "test-key";
        _refundServiceMock.Setup(x => x.ProcessRefundAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot refund more than original amount"));

        // Act
        var result = await _controller.ProcessRefund(Guid.NewGuid(), new RefundRequest { Amount = 500, RefundType = "full" }, default);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPaymentById_InternalError_Returns500()
    {
        // Arrange
        var id = Guid.NewGuid();
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("DB Error"));

        // Act
        var result = await _controller.GetPaymentById(id, default);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task ProcessRefund_InternalError_Returns500()
    {
        // Arrange
        _controller.Request.Headers["Idempotency-Key"] = "test-key";
        _refundServiceMock.Setup(x => x.ProcessRefundAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Error"));

        // Act
        var result = await _controller.ProcessRefund(Guid.NewGuid(), new RefundRequest { Amount = 10, RefundType = "full" }, default);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    private PaymentProvider CreateTestProvider(string name)
    {
        return new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = name,
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "THB" },
            Priority = 1,
            Credentials = new Dictionary<string, string>(),
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
