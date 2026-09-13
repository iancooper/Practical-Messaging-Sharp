using Confluent.Kafka;
using SimpleMessaging;

namespace SimpleEventing;

/// <summary>A record read off the stream, and where it was. The position is ours, not yours.</summary>
public sealed class StreamRecord<T>
{
    public T Message { get; }

    /// <summary>"p1@42" -- the partition and offset, for printing.</summary>
    public string Where { get; }

    /// <summary>Where this record is. Internal: the application never sees a Kafka type.</summary>
    internal TopicPartitionOffset Position { get; }

    /// <summary>
    /// Where the *next* record is, which is what a commit actually means.
    ///
    /// **A committed offset is "the next one I have not read", not "the last one I did read".**
    /// Commit this record's own offset and you have told the broker to start here again, so
    /// every restart replays the record you just finished -- which looks exactly like the
    /// duplicate Probe D is about, and is not it. Off by one, and the symptom is somebody
    /// else's bug.
    /// </summary>
    internal TopicPartitionOffset Next { get; }

    internal StreamRecord(T message, string where, TopicPartitionOffset position)
    {
        Message = message;
        Where = where;
        Position = position;
        Next = new TopicPartitionOffset(position.TopicPartition, position.Offset + 1);
    }
}

/// <summary>
/// The same gateway as <see cref="EventStreamConsumer{T}"/>, with one difference that is the
/// whole reason it exists: **the caller commits.**
///
/// EventStreamConsumer does Get, Translate, Dispatch, Handle and then commits for you, which is
/// the right shape when the ordering is not what you are studying. In exercise 4 the ordering
/// *is* what you are studying -- Probe D is "apply the record, then commit the offset, and die
/// in between" -- so the commit has to be a line in the application that you can move.
///
/// Notice that this is a *gateway* decision, not an application one. The application still
/// names no Kafka type: it gets a StreamRecord, and it says Commit or Seek.
/// </summary>
public sealed class EventStreamReader<T> : IDisposable where T : IAmAMessage
{
    private readonly Func<string, T> _mapper;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic = Stream.TopicFor<T>();

    public static async Task<EventStreamReader<T>> CreateAsync(
        Func<string, T> mapper,
        string consumerGroup,
        string bootstrapServers = Stream.BootstrapServers)
    {
        await Stream.EnsureTopicExists(Stream.TopicFor<T>(), bootstrapServers);
        return new EventStreamReader<T>(mapper, consumerGroup, bootstrapServers);
    }

    private EventStreamReader(Func<string, T> mapper, string consumerGroup, string bootstrapServers)
    {
        _mapper = mapper;

        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = consumerGroup,
            // The local copy is built by replaying the whole log, which is the thing a stream
            // can do and a queue cannot. A new consumer with an empty database reads from the
            // start and catches up; that is Archive and Replay from exercise 3, earning its keep.
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        _consumer.Subscribe(_topic);
    }

    public string Topic => _topic;

    /// <summary>
    /// Get and Translate. Returns null when there was nothing to read, or when the broker was
    /// unhappy in a way that is not this record's fault -- a rebalance, a topic that does not
    /// exist yet. Both are normal and the caller should just come round again.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The record could not be mapped. There is no invalid-record topic on a stream and nothing
    /// will move it to one for you, which is exercise 3's finding and not this exercise's
    /// problem to solve -- so this is fatal, loudly, rather than quietly skipped.
    /// </exception>
    public StreamRecord<T>? Read(CancellationToken cancellationToken)
    {
        ConsumeResult<string, string> result;
        try
        {
            result = _consumer.Consume(cancellationToken);
        }
        catch (ConsumeException e)
        {
            Console.WriteLine($"  consume failed: {e.Error.Reason} -- retrying");
            return null;
        }

        if (result?.Message is null) return null;

        var where = $"p{result.Partition.Value}@{result.Offset.Value}";
        try
        {
            return new StreamRecord<T>(_mapper(result.Message.Value), where, result.TopicPartitionOffset);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"cannot read the record at {where}: {e.Message}. There is no invalid-record " +
                "topic on a stream -- see exercise 3.", e);
        }
    }

    /// <summary>Move the bookmark. Everything up to and including this record is done.</summary>
    public void Commit(StreamRecord<T> record) => _consumer.Commit([record.Next]);

    /// <summary>Wind the bookmark back to this record and read it again.</summary>
    public void Seek(StreamRecord<T> record) => _consumer.Seek(record.Position);

    public void Dispose()
    {
        // Leave the group tidily so the next run does not wait for a session timeout.
        try { _consumer.Close(); } catch (ObjectDisposedException) { /* already gone */ }
        _consumer.Dispose();
    }
}
