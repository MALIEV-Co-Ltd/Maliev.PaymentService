using System.Security.Claims;
using Maliev.MessagingContracts.Generated;
using Maliev.PaymentService.Api.Clients;
using Maliev.PaymentService.Api.Controllers;
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

public class SlipUploadTests
{
    private readonly Mock<IPaymentService> _paymentServiceMock = new();
    private readonly Mock<IRefundService> _refundServiceMock = new();
    private readonly Mock<IPaymentRoutingService> _routingServiceMock = new();
    private readonly Mock<IMetricsService> _metricsServiceMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<PaymentsController>> _loggerMock = new();
    private readonly Mock<IUploadServiceClient> _uploadServiceMock = new();
    private readonly Mock<IChatbotServiceClient> _chatbotServiceMock = new();
    private readonly Mock<IEventPublisher> _eventPublisherMock = new();
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    
    private readonly PaymentsController _controller;
    private readonly Guid _testPaymentId = Guid.NewGuid();

    public SlipUploadTests()
    {
        _controller = new PaymentsController(
            _paymentServiceMock.Object,
            _refundServiceMock.Object,
            _routingServiceMock.Object,
            _metricsServiceMock.Object,
            _cacheMock.Object,
            _loggerMock.Object,
            _uploadServiceMock.Object,
            _chatbotServiceMock.Object,
            _eventPublisherMock.Object,
            _paymentRepositoryMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "customer-123")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task UploadSlip_PaymentNotFound_Returns404()
    {
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction)null!);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        
        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task UploadSlip_InvalidStatus_Returns409()
    {
        var payment = CreateTestPayment(PaymentStatus.Completed);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        
        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.NotNull(conflictResult.Value);
    }

    [Fact]
    public async Task UploadSlip_InvalidFileSize_Returns400()
    {
        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task UploadSlip_InvalidFileType_Returns400()
    {
        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task UploadSlip_EmployeePermission_BypassOwnership_Returns200()
    {
        var employeeUser = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-456"),
            new Claim("permissions", Maliev.PaymentService.Api.Authorization.PaymentPermissions.PaymentsSlipUpload)
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = employeeUser }
        };

        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("slip.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _uploadServiceMock.Setup(x => x.UploadSlipAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", _testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage/slip.jpg");

        _chatbotServiceMock.Setup(x => x.AnalyzeSlipAsync("https://storage/slip.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlipAnalysisResult { IsValid = true, ExtractedAmountThb = 10m, BankName = "Bank", TransferDate = "2026-01-01", Notes = "" });

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SlipUploadResponse>(okResult.Value);
        Assert.Equal("Completed", response.Status);
    }

    [Fact]
    public async Task UploadSlip_OtherCustomer_NoPermission_Returns403()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "customer-999") 
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("slip.jpg");

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var forbiddenResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, forbiddenResult.StatusCode);
    }

    [Fact]
    public async Task UploadSlip_LLMUnavailable_DegradesGracefully()
    {
        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("slip.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _uploadServiceMock.Setup(x => x.UploadSlipAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", _testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage/slip.jpg");

        _chatbotServiceMock.Setup(x => x.AnalyzeSlipAsync("https://storage/slip.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlipAnalysisResult { IsValid = false, Notes = "Verification service unavailable." });

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SlipUploadResponse>(okResult.Value);
        Assert.Equal("PendingVerification", response.Status);
        Assert.False(response.AutoVerified);
    }

    [Fact]
    public async Task UploadSlip_AmountMismatch_SetsPendingVerification()
    {
        var payment = CreateTestPayment(PaymentStatus.Pending);
        payment.Amount = 500m;
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("slip.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _uploadServiceMock.Setup(x => x.UploadSlipAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", _testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage/slip.jpg");

        _chatbotServiceMock.Setup(x => x.AnalyzeSlipAsync("https://storage/slip.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlipAnalysisResult { IsValid = true, ExtractedAmountThb = 100m, BankName = "Bank", TransferDate = "2026-01-01", Notes = "Amount less than required" });

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SlipUploadResponse>(okResult.Value);
        Assert.Equal("PendingVerification", response.Status);
        Assert.False(response.AutoVerified);
    }

    [Fact]
    public async Task UploadSlip_Success_Returns200()
    {
        var payment = CreateTestPayment(PaymentStatus.Pending);
        _paymentServiceMock.Setup(x => x.GetPaymentByIdAsync(_testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.FileName).Returns("slip.jpg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _uploadServiceMock.Setup(x => x.UploadSlipAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg", _testPaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage/slip.jpg");

        _chatbotServiceMock.Setup(x => x.AnalyzeSlipAsync("https://storage/slip.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlipAnalysisResult { IsValid = true, ExtractedAmountThb = 10m, BankName = "Bank", TransferDate = "2026-01-01", Notes = "" });

        var result = await _controller.UploadSlip(_testPaymentId, fileMock.Object, default);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SlipUploadResponse>(okResult.Value);
        Assert.Equal("Completed", response.Status);
        Assert.Equal("https://storage/slip.jpg", response.SlipUrl);
        Assert.True(response.AutoVerified);
        
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<PaymentCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _paymentRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private PaymentTransaction CreateTestPayment(PaymentStatus status)
    {
        return new PaymentTransaction
        {
            Id = _testPaymentId,
            Status = status,
            Amount = 10,
            Currency = "THB",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "k",
            CustomerId = "customer-123",
            OrderId = "o",
            Description = "d",
            PaymentProviderId = Guid.NewGuid(),
            ProviderName = "stripe",
            ProviderTransactionId = "p",
            ReturnUrl = "r",
            CancelUrl = "c",
            CorrelationId = "cor"
        };
    }
}
