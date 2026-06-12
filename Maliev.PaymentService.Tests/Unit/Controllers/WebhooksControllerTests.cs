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
        _processingServiceMock
            .Setup(x => x.ProcessWebhookAsync(It.IsAny<WebhookEvent>()))
            .ReturnsAsync(new WebhookProcessingResult { Success = true, IsDuplicate = false });

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
    public async Task ReceiveWebhook_WaitsForProcessingBeforeAcknowledgingProvider()
    {
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock
            .Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowProcessingToFinish = new TaskCompletionSource<WebhookProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _processingServiceMock
            .Setup(x => x.ProcessWebhookAsync(It.IsAny<WebhookEvent>()))
            .Returns(async () =>
            {
                processingStarted.SetResult();
                return await allowProcessingToFinish.Task;
            });

        var json = JsonSerializer.SerializeToElement(new { id = "evt_wait", type = "checkout.session.completed" });

        var receiveTask = _controller.ReceiveWebhook("stripe", json);
        await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.False(receiveTask.IsCompleted);

        allowProcessingToFinish.SetResult(new WebhookProcessingResult { Success = true, IsDuplicate = false });
        var result = await receiveTask;

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReceiveWebhook_ProcessingFailure_ReturnsInternalServerErrorForProviderRetry()
    {
        var provider = CreateTestProvider("stripe");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock
            .Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _processingServiceMock
            .Setup(x => x.ProcessWebhookAsync(It.IsAny<WebhookEvent>()))
            .ReturnsAsync(new WebhookProcessingResult
            {
                Success = false,
                IsDuplicate = false,
                ErrorMessage = "Failed to update payment state"
            });

        var json = JsonSerializer.SerializeToElement(new { id = "evt_fail", type = "checkout.session.completed" });

        var result = await _controller.ReceiveWebhook("stripe", json);

        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
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

        _controller.Request.Headers["Omise-Signature"] = "omise-sig";

        // Act
        var result = await _controller.ReceiveWebhook("omise", json);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.Is<WebhookEvent>(e => e.Signature == "omise-sig")), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_OmiseProvider_ExtractsEventKeyAsEventType()
    {
        var provider = CreateTestProvider("omise");
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("omise")).ReturnsAsync(provider);
        _validationServiceMock
            .Setup(x => x.ValidateWebhookAsync(
                It.IsAny<PaymentProvider>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);

        var json = JsonSerializer.SerializeToElement(new
        {
            @object = "event",
            id = "evnt_test_charge_complete",
            key = "charge.complete",
            data = new
            {
                @object = "charge",
                id = "chrg_test_123",
                status = "successful"
            }
        });

        var result = await _controller.ReceiveWebhook("omise", json);

        Assert.IsType<OkObjectResult>(result.Result);
        _webhookRepositoryMock.Verify(
            x => x.AddAsync(
                It.Is<WebhookEvent>(e => e.EventType == "charge.complete"),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
    public async Task ReceiveWebhook_ExistingFailedEvent_ReprocessesBeforeAcknowledgingProvider()
    {
        var provider = CreateTestProvider("stripe");
        var existingEvent = CreateTestWebhookEvent(provider.Id, "retry_1", WebhookProcessingStatus.Failed);
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock
            .Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _webhookRepositoryMock
            .Setup(x => x.GetByProviderEventIdAsync(provider.Id, "retry_1"))
            .ReturnsAsync(existingEvent);

        var json = JsonSerializer.SerializeToElement(new { id = "retry_1", type = "checkout.session.completed" });

        var result = await _controller.ReceiveWebhook("stripe", json);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<WebhookReceivedResponse>(okResult.Value);
        Assert.False(response.IsDuplicate);
        Assert.Equal(existingEvent.Id, response.WebhookEventId);
        _webhookRepositoryMock.Verify(x => x.AddAsync(It.IsAny<WebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _processingServiceMock.Verify(x => x.ProcessWebhookAsync(existingEvent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_ExistingFailedEventProcessingFails_ReturnsInternalServerErrorForProviderRetry()
    {
        var provider = CreateTestProvider("stripe");
        var existingEvent = CreateTestWebhookEvent(provider.Id, "retry_fail", WebhookProcessingStatus.Failed);
        _providerRepositoryMock.Setup(x => x.GetByNameAsync("stripe")).ReturnsAsync(provider);
        _validationServiceMock
            .Setup(x => x.ValidateWebhookAsync(It.IsAny<PaymentProvider>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _webhookRepositoryMock
            .Setup(x => x.GetByProviderEventIdAsync(provider.Id, "retry_fail"))
            .ReturnsAsync(existingEvent);
        _processingServiceMock
            .Setup(x => x.ProcessWebhookAsync(existingEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult
            {
                Success = false,
                IsDuplicate = false,
                ErrorMessage = "Failed again"
            });

        var json = JsonSerializer.SerializeToElement(new { id = "retry_fail", type = "checkout.session.completed" });

        var result = await _controller.ReceiveWebhook("stripe", json);

        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
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

    private static WebhookEvent CreateTestWebhookEvent(
        Guid providerId,
        string providerEventId,
        WebhookProcessingStatus status)
    {
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            ProviderEventId = providerEventId,
            EventType = "checkout.session.completed",
            RawPayload = JsonSerializer.Serialize(new { id = providerEventId, type = "checkout.session.completed" }),
            SignatureValidated = true,
            ProcessingStatus = status,
            ProcessingAttempts = status == WebhookProcessingStatus.Failed ? 1 : 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
    }
}
