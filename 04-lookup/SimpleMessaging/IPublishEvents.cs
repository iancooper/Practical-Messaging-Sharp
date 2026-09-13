namespace SimpleMessaging;

/// <summary>
/// Somewhere to put a fact that has happened. The handler depends on this and not on Kafka,
/// which is the same separation the Message Mapper bought on the way in.
///
/// **That separation does not save you here, and it is important to see why.** The handler is
/// clean, the domain has no broker in it, and the code reads beautifully -- and the problem in
/// exercise 3 is not in any of that. It is in the fact that a write to a broker and an
/// acknowledgement to a different broker cannot be made to happen together.
/// </summary>
public interface IPublishEvents<in T> where T : IAmAMessage
{
    Task Publish(T @event);
}
