using Maliev.PaymentService.Core.Interfaces;
using MassTransit;

namespace Maliev.PaymentService.Infrastructure.Messaging;

/// <summary>
/// MassTransit implementation of IEventPublisher for publishing events to RabbitMQ.
/// </summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    /// <summary>
    /// Publishes an event to the message queue using MassTransit.
    /// </summary>
    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        await _publishEndpoint.Publish(@event, cancellationToken);
    }
}
