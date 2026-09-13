namespace SimpleMessaging;

/// <summary>
/// The contract your application code implements so the pump can dispatch to it.
///
/// A domain type in, and nothing else. No delivery, no channel, no headers, no ack. The
/// handler does not know the pump exists, which is exactly why a test can call it too.
/// </summary>
public interface IAmAHandler<in T> where T : IAmAMessage
{
    Task Handle(T message);
}
