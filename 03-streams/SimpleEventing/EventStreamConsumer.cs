using Confluent.Kafka;

namespace SimpleEventing;

/// <summary>
/// Reads records from a Kafka topic and hands them to application code.
///
/// It is the same four stages as the message pump -- Get, Translate, Dispatch, Handle -- and
/// then the fifth thing, the one that decides everything about failure, is different:
///
///   On a queue:  acknowledge this message. The broker holds the others.
///   On a stream: commit this offset. It means "I am past everything up to here."
///
/// **An offset is a bookmark, not a lock.** There is no per-record acknowledgement, so there is
/// nothing to withhold for one record and grant for another. You are either past a point in the
/// log or you are not.
///
/// Which means every mechanism exercise 2 relied on is simply absent:
///
///   requeue                 -- nothing to hand back
///   requeue with delay      -- nothing holding it, so nothing to hold it longer
///   reject                  -- nothing to route it away
///   dead letter queue       -- nothing to move it there
///   redelivery count        -- nothing counting
///
/// This consumer takes the default answer, which is the one most frameworks take for you:
/// **retry in place.** Read what that does to a partition, then read PROBE.md.
/// </summary>
public sealed class EventStreamConsumer<T> : IDisposable where T : SimpleMessaging.IAmAMessage
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly Func<string, T> _mapper;
    private readonly Func<T, Task> _handler;
    private readonly IConsumer<string, string> _consumer;

    public static async Task<EventStreamConsumer<T>> CreateAsync(
        Func<string, T> mapper,
        Func<T, Task> handler,
        string consumerGroup = Stream.ConsumerGroup,
        string bootstrapServers = Stream.BootstrapServers)
    {
        // Either end may create the topic, so it does not matter which you start first.
        await Stream.EnsureTopicExists(Stream.TopicFor<T>(), bootstrapServers);
        return new EventStreamConsumer<T>(mapper, handler, consumerGroup, bootstrapServers);
    }

    private EventStreamConsumer(
        Func<string, T> mapper,
        Func<T, Task> handler,
        string consumerGroup,
        string bootstrapServers)
    {
        _mapper = mapper;
        _handler = handler;

        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = consumerGroup,
            // Start at the beginning of the log the first time this group ever reads it.
            // A queue has no equivalent of this setting, because a queue has no past.
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Commit when we say so. Auto-commit on a timer would move the bookmark past
            // records we have not finished with, which is the stream's version of acking early.
            EnableAutoCommit = false
        }).Build();

        _consumer.Subscribe(Stream.TopicFor<T>());
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Following {Stream.TopicFor<T>()} as group '{Stream.ConsumerGroup}'");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // GET
                ConsumeResult<string, string> result;
                try
                {
                    result = _consumer.Consume(cancellationToken);
                }
                catch (ConsumeException e)
                {
                    // Broker-level, not record-level: the topic is not there yet, a rebalance
                    // is in progress, the broker is restarting. None of those are this
                    // record's fault, because there is no record.
                    Console.WriteLine($"  consume failed: {e.Error.Reason} -- retrying");
                    await Task.Delay(RetryDelay, cancellationToken);
                    continue;
                }

                if (result?.Message is null) continue;

                var where = $"p{result.Partition.Value}@{result.Offset.Value}";

                try
                {
                    // TRANSLATE
                    var message = _mapper(result.Message.Value);

                    // DISPATCH and HANDLE
                    await _handler(message);

                    // Move the bookmark. Everything up to and including this offset is done.
                    _consumer.Commit(result);
                    Console.WriteLine($"  committed {where}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  FAILED {where}: {e.Message}");
                    Console.WriteLine("  there is no nack, no requeue and no dead letter topic, so: retry in place");

                    // Wind the bookmark back to this record and read it again. The partition
                    // stops here until this record succeeds -- which, if it never can, is
                    // forever. The other partitions carry on, perfectly happily.
                    _consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected ending.
        }
        finally
        {
            // Leave the group tidily so the next run does not wait for a session timeout.
            _consumer.Close();
        }
    }

    public void Dispose() => _consumer.Dispose();
}
