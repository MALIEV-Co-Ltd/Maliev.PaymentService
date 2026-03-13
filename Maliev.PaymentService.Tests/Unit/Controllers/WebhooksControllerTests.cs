using System.Text.Json;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Controllers;

public class WebhooksControllerTests
{
    private readonly Mock<IProviderRepository> _providerRepositoryMock = new();
    private readonly Mock<IWebhookRepository> _webhookRepositoryMock = new();
    private readonly Mock<IWebhookValidationService> _validationServiceMock = new();
    private readonly Mock<IWebhookProcessingService> _processingServiceMock = new();
    private readonly Mock<IMetricsService> _metricsServiceMock = new();
    private readonly Mock<ILogger<WebhooksController>> _loggerMock = new();
    private readonly WebhooksController _controller;

    public WebhooksControllerTests()
    {
        _controller = new WebhooksController(
            _providerRepositoryMock.Object,
            _webhookRepositoryMock.Object,
            _validationServiceMock.Object,
            _processingServiceMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task ReceiveWebhook_InternalError_Returns500()
    {
        // Arrange
        _providerRepositoryMock.Setup(x => x.GetByNameAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("DB failure"));

        var json = JsonSerializer.SerializeToElement(new { id = "1" });

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_PayPalProvider_ExtractsIdCorrectly()
    {
        // Arrange
        var provider = CreateTestProvider("paypal");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("paypal")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { id = "pp_evt_1", event_type = "PAYMENT.SALE.COMPLETED" };
        var json = JsonSerializer.SerializeToElement(payload);

        // Act
        var result = await _controller.ReceiveWebhook("paypal", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.ProviderEventId == "pp_evt_1")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_StripeProvider_ExtractsSignatureCorrectly()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { id = "evt_1", type = "payment_intent.succeeded" };
        var json = JsonSerializer.SerializeToElement(payload);

        _controller.Request.Headers["Stripe-Signature"] = "t=123,v1=sig";

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.Signature == "t=123,v1=sig")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_OmiseProvider_ExtractsSignatureCorrectly()
    {
        // Arrange
        var provider = CreateTestProvider("omise");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("omise")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { id = "ev_1", type = "charge.complete" };
        var json = JsonSerializer.SerializeToElement(payload);

        _controller.Request.Headers["X-Omise-Signature"] = "omise-sig";

        // Act
        var result = await _controller.ReceiveWebhook("omise", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.Signature == "omise-sig")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_ScbProvider_ExtractsSignatureCorrectly()
    {
        // Arrange
        var provider = CreateTestProvider("scb");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("scb")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { event_id = "scb_1", type = "BILL_PAYMENT" };
        var json = JsonSerializer.SerializeToElement(payload);

        _controller.Request.Headers["X-SCB-Signature"] = "scb-sig";

        // Act
        var result = await _controller.ReceiveWebhook("scb", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.Signature == "scb-sig")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_DuplicateDetected_ReturnsOkWithIsDuplicate()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var existingEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProviderId = provider.Id,
            ProviderEventId = "dup_1",
            EventType = "test",
            RawPayload = "{}",
            SignatureValidated = true,
            ProcessingStatus = WebhookProcessingStatus.Completed,
            ProcessingAttempts = 1,
            UpdatedAt = DateTime.UtcNow
        };
        _webhookRepositoryMock.Setup(x => x.GetByProviderEventIdAsync(provider.Id, "dup_1")).ReturnsAsync(existingEvent);

        var json = JsonSerializer.SerializeToElement(new { id = "dup_1", type = "test" });

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WebhookReceivedResponse>(okResult.Value);
        Assert.True(response.IsDuplicate);
    }

    [Fact]
    public async Task ReceiveWebhook_CamelCaseEventType_ExtractsCorrectly()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { id = "evt_1", eventType = "payment.succeeded" };
        var json = JsonSerializer.SerializeToElement(payload);

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.EventType == "payment.succeeded")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_UnknownEventType_ReturnsUnknown()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var payload = new { id = "evt_1" }; // No type
        var json = JsonSerializer.SerializeToElement(payload);

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.EventType == "unknown")), Times.Once);
    }

    [Fact]
    public async Task TestWebhook_ReturnsOk()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            var provider = CreateTestProvider("stripe");
            _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
            _processingServiceMock.Setup(x => x.ProcessWebhookAsync(It.IsAny<WebhookEvent>()))
                .ReturnsAsync(new WebhookProcessingResult { Success = true, IsDuplicate = false });

            var request = new TestWebhookRequest { EventType = "test", TransactionId = Guid.NewGuid() };

            // Act
            var result = await _controller.TestWebhook("stripe", request);

            // Assert
            Assert.IsType<OkObjectResult>(result.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public async Task TestWebhook_Production_ReturnsForbidden()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        try
        {
            var request = new TestWebhookRequest { EventType = "test" };

            // Act
            var result = await _controller.TestWebhook("stripe", request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, statusCodeResult.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public async Task ReceiveWebhook_UnknownProvider_ReturnsBadRequest()
    {
        // Arrange
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("unknown")).ReturnsAsync((PaymentProvider?)null);
        var json = JsonSerializer.SerializeToElement(new { id = "1" });

        // Act
        var result = await _controller.ReceiveWebhook("unknown", json);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(false); // Invalid

        var json = JsonSerializer.SerializeToElement(new { id = "1" });

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingEventId_ReturnsBadRequest()
    {
        // Arrange
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock.Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var json = JsonSerializer.SerializeToElement(new { some_other_field = "no-id" });

        // Act
        var result = await _controller.ReceiveWebhook("stripe", json);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task TestWebhook_UnknownProvider_ReturnsBadRequest()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            _providerRepositoryMock.Setup(x => x.GetByNameAsync("unknown")).ReturnsAsync((PaymentProvider?)null);
            var request = new TestWebhookRequest { EventType = "test" };

            // Act
            var result = await _controller.TestWebhook("unknown", request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
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
