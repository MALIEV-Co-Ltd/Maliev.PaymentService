namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Interface for publishing domain events to the message queue.
/// Abstracts MassTransit implementation to maintain clean architecture.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to the message queue.
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="event">Event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
}
