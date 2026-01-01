namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Interface for publishing domain events to message broker.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to the message broker.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
