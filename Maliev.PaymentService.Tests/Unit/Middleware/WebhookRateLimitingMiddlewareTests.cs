using Maliev.PaymentService.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Middleware;

public class WebhookRateLimitingMiddlewareTests
{
    private readonly Mock<RequestDelegate> _nextMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<WebhookRateLimitingMiddleware>> _loggerMock = new();
    private readonly WebhookRateLimitingMiddleware _middleware;

    public WebhookRateLimitingMiddlewareTests()
    {
        _middleware = new WebhookRateLimitingMiddleware(_nextMock.Object, _cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_NonWebhookPath_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/payments";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WebhooksPathWithoutProvider_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/payment/v1/webhooks/";

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CacheThrowsException_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/payment/v1/webhooks/stripe";
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ThrowsAsync(new System.Exception("Redis down"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextMock.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitExceeded_Returns429()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/payment/v1/webhooks/stripe";
        context.Response.Body = new MemoryStream();

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("100"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        _nextMock.Verify(x => x(context), Times.Never);
    }
}
