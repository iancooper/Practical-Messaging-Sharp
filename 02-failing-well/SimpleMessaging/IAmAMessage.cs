namespace SimpleMessaging;

/// <summary>
/// Anything we are willing to put on a channel.
/// The Id is the message's identity, not the entity's -- we need it to key a stream,
/// and later to answer "have I seen this before?".
/// </summary>
public interface IAmAMessage
{
    string Id { get; }
}
