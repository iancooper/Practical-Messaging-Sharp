using SimpleMessaging;

namespace SimpleEventing;

/// <summary>
/// Implements <see cref="IPublishEvents{T}"/> over a Kafka topic. This is the only place the
/// handler's "tell the world" becomes "append to a log".
/// </summary>
public sealed class KafkaEventPublisher<T>(EventStreamProducer<T> producer)
    : IPublishEvents<T>, IDisposable where T : IAmAMessage
{
    public Task Publish(T @event) => producer.Send(@event);

    public void Dispose() => producer.Dispose();
}
