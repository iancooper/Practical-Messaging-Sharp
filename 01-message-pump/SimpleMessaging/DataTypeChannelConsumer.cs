using RabbitMQ.Client;

namespace SimpleMessaging;

/// <summary>
/// The consumer half of the Messaging Gateway. Again, the only class that knows this is RabbitMQ.
///
/// Under RMQ, to receive, we:
///     1. open a socket connection to the broker
///     2. create a channel on that socket
///     3. declare the same direct exchange the producer publishes to
///     4. declare a queue to hold our messages
///     5. bind the queue to the routing key on that exchange
///
/// Both ends declare the exchange, so it does not matter which starts first. Only we declare
/// the queue -- which does mean that anything published before the first run of the consumer
/// went nowhere. Start the consumer once before you send anything.
///
/// This is a Polling Consumer: <see cref="Receive"/> asks the broker whether there is anything
/// there. It costs us a thread while idle and buys us not having to hold a connection open for
/// the broker to call back on.
///
/// This class is given to you and it is correct. Read it for the AMQP vocabulary; the
/// exercise is not here.
/// </summary>
public sealed class DataTypeChannelConsumer<T> : IAsyncDisposable where T : IAmAMessage
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queueName = Channel.QueueNameFor<T>();

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

        var routingKey = Channel.RoutingKeyFor<T>();
        var queueName = Channel.QueueNameFor<T>();

        await channel.ExchangeDeclareAsync(Channel.ExchangeName, ExchangeType.Direct, durable: true);

        // Durable queue to go with the persistent messages: no point writing a message to disk
        // and then keeping it in a queue that evaporates on restart.
        await channel.QueueDeclareAsync(
            queue: queueName, durable: true, exclusive: false, autoDelete: false);

        await channel.QueueBindAsync(queue: queueName, exchange: Channel.ExchangeName, routingKey: routingKey);

        return new DataTypeChannelConsumer<T>(connection, channel);
    }

    /// <summary>
    /// Ask the broker for one message. Null means the queue was empty.
    ///
    /// autoAck is false, so what comes back is *locked to us* and not yet removed from the
    /// queue. The broker is now waiting to be told what happened. Until we tell it, this
    /// message shows in the management console as "unacked".
    /// </summary>
    public Task<BasicGetResult?> Receive() => _channel.BasicGetAsync(_queueName, autoAck: false);

    /// <summary>I am done with this message. The broker may forget it.</summary>
    public ValueTask Acknowledge(ulong deliveryTag) =>
        _channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false);

    /// <summary>
    /// I am not done with this message.
    /// requeue: true  -- put it back, someone will try again (possibly us, immediately).
    /// requeue: false -- reject it. On a plain queue that deletes it.
    /// </summary>
    public ValueTask Reject(ulong deliveryTag, bool requeue) =>
        _channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: requeue);

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
