using Confluent.Kafka;

namespace SimpleEventing;

/// <summary>
/// Appends records to a Kafka topic. The producer half of the eventing gateway.
///
/// Note what is *not* here, compared with the RabbitMQ producer: no exchange, no binding, no
/// routing key. You append to a log, and the key decides which partition the record lands in.
/// Same key, same partition, so records that must stay in order must share a key.
/// </summary>
public sealed class EventStreamProducer<T> : IDisposable where T : SimpleMessaging.IAmAMessage
{
    private readonly Func<T, string> _serializer;
    private readonly Func<T, string> _partitionKey;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic = Stream.TopicFor<T>();

    private EventStreamProducer(
        Func<T, string> serializer, Func<T, string> partitionKey, IProducer<string, string> producer)
    {
        _serializer = serializer;
        _partitionKey = partitionKey;
        _producer = producer;
    }

    /// <param name="serializer">Turns a T into the record's value.</param>
    /// <param name="partitionKey">
    /// Which partition this record belongs in -- and therefore what it is ordered with respect
    /// to. Records sharing a key share a partition and stay in order; records with different
    /// keys have no order between them at all.
    ///
    /// **This is a design decision and there is no safe default**, which is why you have to
    /// pass it. Key an order's events by the order and they arrive in sequence. Key them by
    /// the event's own id and every event is independent -- which is fine right up until two
    /// events about the same thing are processed out of order by different consumers.
    /// </param>
    public static async Task<EventStreamProducer<T>> CreateAsync(
        Func<T, string> serializer,
        Func<T, string> partitionKey,
        string bootstrapServers = Stream.BootstrapServers)
    {
        await Stream.EnsureTopicExists(Stream.TopicFor<T>(), bootstrapServers);

        var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            // Wait for the leader and all in-sync replicas before calling a write done.
            // This is the producer-side half of guaranteed delivery, and it is the cheap half --
            // exactly as it was on RabbitMQ, where it was one 'persistent' flag.
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();

        return new EventStreamProducer<T>(serializer, partitionKey, producer);
    }

    /// <summary>
    /// Append a record, and wait until the broker has acknowledged it.
    ///
    /// <c>ProduceAsync</c> rather than <c>Produce</c>: fire-and-forget would return before the
    /// record was durable, and then "I produced the event" would be a claim about a buffer in
    /// this process rather than about anything the broker has.
    /// </summary>
    public async Task Send(T message)
    {
        var result = await _producer.ProduceAsync(_topic, new Message<string, string>
        {
            Key = _partitionKey(message),
            Value = _serializer(message)
        });

        Console.WriteLine($"  -> {result.Topic} partition {result.Partition.Value} offset {result.Offset.Value}");
    }

    /// <summary>Append a record we did not serialize -- used to put something unreadable on the stream.</summary>
    public async Task SendRaw(string key, string body)
    {
        var result = await _producer.ProduceAsync(_topic, new Message<string, string> { Key = key, Value = body });
        Console.WriteLine($"  -> {result.Topic} partition {result.Partition.Value} offset {result.Offset.Value}");
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
