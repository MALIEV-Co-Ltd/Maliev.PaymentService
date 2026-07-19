using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class WebhookRetryServiceTests
{
    [Fact]
    public async Task ProcessDueRetriesAsync_DueFailedWebhook_RetriesByWebhookId()
    {
        var webhook = CreateFailedWebhook();
        var webhookRepositoryMock = new Mock<IWebhookRepository>();
        var processingServiceMock = new Mock<IWebhookProcessingService>();

        webhookRepositoryMock
            .Setup(repository => repository.GetPendingRetriesAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebhookEvent> { webhook });

        processingServiceMock
            .Setup(service => service.RetryWebhookAsync(webhook.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult { Success = true, IsDuplicate = false });

        var services = new ServiceCollection();
        services.AddScoped(_ => webhookRepositoryMock.Object);
        services.AddScoped(_ => processingServiceMock.Object);

        await using var serviceProvider = services.BuildServiceProvider();
        var retryService = new WebhookRetryService(
            serviceProvider,
            Mock.Of<ILogger<WebhookRetryService>>());

        var processedCount = await retryService.ProcessDueRetriesAsync(CancellationToken.None);

        Assert.Equal(1, processedCount);
        processingServiceMock.Verify(
            service => service.RetryWebhookAsync(webhook.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static WebhookEvent CreateFailedWebhook()
    {
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            ProviderEventId = "evt_retry",
            EventType = "checkout.session.completed",
            RawPayload = "{}",
            SignatureValidated = true,
            ProcessingStatus = WebhookProcessingStatus.Failed,
            ProcessingAttempts = 1,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
    }
}
