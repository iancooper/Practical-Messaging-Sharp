namespace SimpleMessaging;

/// <summary>
/// The names both ends of the channel have to agree on, in one place.
///
/// A Datatype Channel carries one type of message, so we derive the routing key from the
/// type. Producer and consumer both compute it, which is how they find each other without
/// a shared config file.
/// </summary>
public static class Channel
{
    public const string ExchangeName = "practical-messaging-pump";

    public static string RoutingKeyFor<T>() => "message-pump." + typeof(T).FullName;

    public static string QueueNameFor<T>() => RoutingKeyFor<T>();
}
