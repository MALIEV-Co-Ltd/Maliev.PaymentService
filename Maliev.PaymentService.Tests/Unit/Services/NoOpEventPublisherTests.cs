using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class NoOpEventPublisherTests
{
    private readonly Mock<ILogger<NoOpEventPublisher>> _loggerMock;
    private readonly NoOpEventPublisher _publisher;

    public NoOpEventPublisherTests()
    {
        _loggerMock = new Mock<ILogger<NoOpEventPublisher>>();
        _publisher = new NoOpEventPublisher(_loggerMock.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotThrow()
    {
        var message = new TestEvent { Id = Guid.NewGuid(), Name = "test" };

        await _publisher.PublishAsync(message);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ShouldNotThrow()
    {
        var message = new TestEvent { Id = Guid.NewGuid(), Name = "test" };
        using var cts = new CancellationTokenSource();

        await _publisher.PublishAsync(message, cts.Token);
    }

    private class TestEvent
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
}
