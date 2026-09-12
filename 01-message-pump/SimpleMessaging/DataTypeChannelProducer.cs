using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging;

/// <summary>
/// The producer half of the Messaging Gateway: the only class here that knows this is RabbitMQ.
///
/// Under RMQ, to send, we:
///     1. open a socket connection to the broker
///     2. create a channel (a lightweight logical connection) on that socket
///     3. declare a direct exchange to publish to
///
/// We do not declare the queue. The consumer does that, and binds it to our routing key.
/// That is the asymmetry AMQP has and a queue API does not: we publish to an exchange and
/// have no idea who, if anyone, is listening.
///
/// This class is given to you and it is correct. Read it for the AMQP vocabulary; the
/// exercise is not here.
/// </summary>
public sealed class DataTypeChannelProducer<T> : IAsyncDisposable where T : IAmAMessage
{
    private readonly Func<T, string> _serializer;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _routingKey = Channel.RoutingKeyFor<T>();

    private DataTypeChannelProducer(Func<T, string> serializer, IConnection connection, IChannel channel)
    {
        _serializer = serializer;
        _connection = connection;
        _channel = channel;
    }

    /// <summary>
    /// Connecting is I/O, and a constructor cannot await, so construction is a static method.
    /// </summary>
    /// <param name="serializer">Turns a T into the string we put in the body.</param>
    public static async Task<DataTypeChannelProducer<T>> CreateAsync(
        Func<T, string> serializer, string hostName = "localhost")
    {
        // Defaults: user guest, password guest, port 5672, virtual host /
        var factory = new ConnectionFactory { HostName = hostName, AutomaticRecoveryEnabled = true };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        // Durable, so the exchange survives a broker restart.
        await channel.ExchangeDeclareAsync(Channel.ExchangeName, ExchangeType.Direct, durable: true);

        return new DataTypeChannelProducer<T>(serializer, connection, channel);
    }

    /// <summary>
    /// Send a message. The routing key is derived from T, so sender and receiver match up
    /// without either knowing about the other.
    /// </summary>
    public async Task Send(T message)
    {
        var body = Encoding.UTF8.GetBytes(_serializer(message));

        // Persistent: the broker writes it to its message store, so it survives a broker restart.
        // This is the producer-side half of guaranteed delivery, and it is the cheap half.
        var properties = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };

        await _channel.BasicPublishAsync(
            exchange: Channel.ExchangeName,
            routingKey: _routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    /// <summary>Send a body we did not serialize -- used to put something unmappable on the channel.</summary>
    public async Task SendRaw(string body)
    {
        var properties = new BasicProperties { DeliveryMode = DeliveryModes.Persistent };
        await _channel.BasicPublishAsync(
            exchange: Channel.ExchangeName,
            routingKey: _routingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(body));
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
