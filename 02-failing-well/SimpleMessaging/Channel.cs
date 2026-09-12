namespace SimpleMessaging;

/// <summary>
/// The topology, in one place -- and there is more of it than there was in exercise 1.
///
/// Four queues now, because a message that is never going to be handled has more than one
/// place it can end up, and *which* place is how you find out what went wrong.
///
///   practical-messaging-failing-well              (direct)
///     `-- failing-well.&lt;T&gt;                  the work
///            x-dead-letter-exchange:    ...dlx
///            x-dead-letter-routing-key: retry.failing-well.&lt;T&gt;
///
///   practical-messaging-failing-well.dlx          (direct)
///     |-- retry.failing-well.&lt;T&gt;            work waiting to be tried again
///     |      x-message-ttl:             5000
///     |      x-dead-letter-exchange:    practical-messaging-failing-well
///     |      x-dead-letter-routing-key: failing-well.&lt;T&gt;
///     |
///     |-- invalid.failing-well.&lt;T&gt;          a body we could not read
///     `-- dead.failing-well.&lt;T&gt;             work we retried and gave up on
///
/// **The retry queue is the part worth understanding, because RabbitMQ does the work and you
/// do not.** Nothing consumes it, and every message in it has five seconds to live. So each
/// message expires -- and an expired message is dead-lettered, and this queue's dead-letter
/// exchange points back at the main exchange. It comes home on a timer nobody wrote.
///
/// That is *Requeue with Delay*, built out of a TTL and a dead-letter exchange. Stock
/// RabbitMQ: no plugin, no scheduler, no code.
///
/// **And notice which way round the two hops go.** Rejecting a message from the work queue
/// sends it to *retry*, not to the dead letter queue -- because the round trip work → retry →
/// work is a cycle RabbitMQ manages end to end, and a cycle it manages is a cycle it will
/// count for you in the `x-death` header. The other two destinations are terminal, so nothing
/// needs counting and we can publish to them directly.
///
/// The two terminal queues should be empty. When they are not, that is the alert.
/// </summary>
public static class Channel
{
    public const string ExchangeName = "practical-messaging-failing-well";
    public const string DeadLetterExchangeName = ExchangeName + ".dlx";

    /// <summary>How long a message waits in the retry queue before it comes back.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static string RoutingKeyFor<T>() => "failing-well." + typeof(T).FullName;
    public static string QueueNameFor<T>() => RoutingKeyFor<T>();

    /// <summary>Work waiting for its next attempt. Should always be nearly empty.</summary>
    public static string RetryQueueNameFor<T>() => "retry." + QueueNameFor<T>();

    /// <summary>Bodies we could not read. Terminal: nothing here is ever retried.</summary>
    public static string InvalidQueueNameFor<T>() => "invalid." + QueueNameFor<T>();

    /// <summary>Work we retried and gave up on. Terminal: an operator's in-tray.</summary>
    public static string DeadLetterQueueNameFor<T>() => "dead." + QueueNameFor<T>();
}
