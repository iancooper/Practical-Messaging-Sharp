using RabbitMQ.Client;

namespace SimpleMessaging;

/// <summary>
/// The consumer half of the Messaging Gateway. It declares the topology described in
/// <see cref="Channel"/> and hands the pump five things it can do with a message.
///
/// **The plumbing is given to you and it is correct.** Declaring exchanges and binding queues
/// is AMQP vocabulary, not judgement, and you can read it here at your leisure. The exercise is
/// deciding *which of these five to call, and when* -- and that lives in the pump.
/// </summary>
public sealed class DataTypeChannelConsumer<T> : IAsyncDisposable where T : IAmAMessage
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queueName = Channel.QueueNameFor<T>();
    private readonly string _invalidQueueName = Channel.InvalidQueueNameFor<T>();
    private readonly string _deadLetterQueueName = Channel.DeadLetterQueueNameFor<T>();

    private DataTypeChannelConsumer(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<DataTypeChannelConsumer<T>> CreateAsync(string hostName = "localhost")
    {
        var factory = new ConnectionFactory { HostName = hostName, AutomaticRecoveryEnabled = true };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        var routingKey  = Channel.RoutingKeyFor<T>();
        var queueName   = Channel.QueueNameFor<T>();
        var invalidKey  = Channel.InvalidQueueNameFor<T>();
        var deadKey     = Channel.DeadLetterQueueNameFor<T>();
        var retryKey    = Channel.RetryQueueNameFor<T>();

        await channel.ExchangeDeclareAsync(Channel.ExchangeName, ExchangeType.Direct, durable: true);
        await channel.ExchangeDeclareAsync(Channel.DeadLetterExchangeName, ExchangeType.Direct, durable: true);

        // The work queue. Rejecting a message from here (nack, requeue:false) sends it to the
        // dead-letter exchange with the *retry* routing key -- so a rejection lands in the
        // retry queue without us publishing anything.
        //
        // Note what this means: the queue's dead-letter routing key is fixed at declare time.
        // One reject, one destination. Anything else you want to do with a message, you do by
        // publishing it somewhere yourself.
        await channel.QueueDeclareAsync(
            queue: queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"]    = Channel.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = retryKey
            });
        await channel.QueueBindAsync(queueName, Channel.ExchangeName, routingKey);

        // Bodies we could not read. A terminal destination: no TTL, no dead-letter exchange.
        await channel.QueueDeclareAsync(
            queue: invalidKey, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(invalidKey, Channel.DeadLetterExchangeName, invalidKey);

        // Work we gave up on. Also terminal. This is the one an operator looks in.
        await channel.QueueDeclareAsync(
            queue: deadKey, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(deadKey, Channel.DeadLetterExchangeName, deadKey);

        // The retry queue: a waiting room with a clock on the door.
        // Nothing consumes it. Every message in it expires after RetryDelay, and an expired
        // message is dead-lettered -- back to the main exchange, and so back to the work queue.
        // RabbitMQ stamps an x-death header on the way through, which is how we count attempts.
        await channel.QueueDeclareAsync(
            queue: retryKey, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"]             = (int)Channel.RetryDelay.TotalMilliseconds,
                ["x-dead-letter-exchange"]    = Channel.ExchangeName,
                ["x-dead-letter-routing-key"] = routingKey
            });
        await channel.QueueBindAsync(retryKey, Channel.DeadLetterExchangeName, retryKey);

        return new DataTypeChannelConsumer<T>(connection, channel);
    }

    /// <summary>Ask the broker for one message. Null means the queue was empty.</summary>
    public Task<BasicGetResult?> Receive() => _channel.BasicGetAsync(_queueName, autoAck: false);

    /// <summary>Done. The broker may forget it.</summary>
    public ValueTask Acknowledge(ulong deliveryTag) =>
        _channel.BasicAckAsync(deliveryTag, multiple: false);

    /// <summary>
    /// Put it back on the queue, right now, for someone to try again immediately.
    /// There is no limit on this and no delay. Think about what that means before you use it.
    /// </summary>
    public ValueTask Requeue(ulong deliveryTag) =>
        _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);

    /// <summary>
    /// Reject it. Because of the work queue's arguments, the broker routes it to the
    /// **retry queue**, where it waits and then comes back on its own. One call, and RabbitMQ
    /// does the moving -- and because RabbitMQ owns both hops, RabbitMQ counts them for you.
    /// </summary>
    public ValueTask RejectForRetry(ulong deliveryTag) =>
        _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);

    /// <summary>
    /// Publish it to the invalid message queue. Terminal: a body nobody can read.
    ///
    /// This is a publish, not a reject -- so the original delivery is still outstanding and it
    /// is still your problem. Headers are carried forward, because x-death is the attempt count
    /// and losing it resets the clock.
    /// </summary>
    public Task SendToInvalidMessageQueue(BasicGetResult delivery) =>
        Republish(delivery, _invalidQueueName);

    /// <summary>Send it to the dead letter queue. Terminal: somebody has to come and look.</summary>
    public Task SendToDeadLetter(BasicGetResult delivery) =>
        Republish(delivery, _deadLetterQueueName);

    private async Task Republish(BasicGetResult delivery, string routingKey)
    {
        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = delivery.BasicProperties.Headers is null
                ? null
                : new Dictionary<string, object?>(delivery.BasicProperties.Headers)
        };

        await _channel.BasicPublishAsync(
            exchange: Channel.DeadLetterExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: delivery.Body);
    }

    /// <summary>
    /// How many times has this message been round the retry loop?
    ///
    /// RabbitMQ records every dead-lettering in an `x-death` header: an array of entries, one
    /// per (queue, reason) pair, each with a count. A message that has expired out of the retry
    /// queue twice has an entry for that queue with count 2. A message arriving for the first
    /// time has no x-death header at all, so it has had no attempts yet.
    ///
    /// Look at this header in the management console. It is the most useful thing RabbitMQ will
    /// tell you about a message's history and almost nobody knows it is there.
    /// </summary>
    public static int RetriesSoFar(BasicGetResult delivery)
    {
        if (delivery.BasicProperties.Headers is null ||
            !delivery.BasicProperties.Headers.TryGetValue("x-death", out var raw) ||
            raw is not IList<object?> deaths)
            return 0;

        var retryQueue = Channel.RetryQueueNameFor<T>();

        foreach (var death in deaths)
        {
            if (death is not IDictionary<string, object?> entry) continue;
            if (Text(entry, "queue") != retryQueue) continue;
            if (entry.TryGetValue("count", out var count))
                return Convert.ToInt32(count);
        }

        return 0;
    }

    private static string? Text(IDictionary<string, object?> entry, string key) =>
        entry.TryGetValue(key, out var value)
            ? value switch
            {
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _            => value?.ToString()
            }
            : null;

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
