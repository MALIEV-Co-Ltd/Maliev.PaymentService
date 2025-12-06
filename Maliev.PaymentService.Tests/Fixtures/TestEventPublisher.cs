using System.Collections.Concurrent;
using Maliev.PaymentService.Core.Interfaces;

namespace Maliev.PaymentService.Tests.Fixtures;

/// <summary>
/// In-memory implementation of IEventPublisher for integration tests.
/// Replaces MassTransit implementation to avoid RabbitMQ dependency.
/// </summary>
public class TestEventPublisher : IEventPublisher
{
    private readonly ConcurrentBag<object> _publishedEvents = new();

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        _publishedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public IEnumerable<object> GetPublishedEvents()
    {
        return _publishedEvents;
    }
}
