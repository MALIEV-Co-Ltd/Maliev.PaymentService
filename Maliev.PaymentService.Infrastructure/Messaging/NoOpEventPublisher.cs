using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Messaging;

/// <summary>
/// No-op event publisher for development mode when RabbitMQ is disabled.
/// Logs events instead of publishing them.
/// </summary>
public class NoOpEventPublisher : IEventPublisher
{
    private readonly ILogger<NoOpEventPublisher> _logger;

    public NoOpEventPublisher(ILogger<NoOpEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogWarning(
            "Event publishing disabled (RabbitMQ not configured). Would have published: {EventType} - {Event}",
            typeof(T).Name,
            System.Text.Json.JsonSerializer.Serialize(message));
        return Task.CompletedTask;
    }
}
