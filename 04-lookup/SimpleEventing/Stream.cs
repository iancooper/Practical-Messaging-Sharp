using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace SimpleEventing;

/// <summary>
/// The stream's names and shape, in one place -- and notice how much shorter this is than
/// <c>SimpleMessaging.Channel</c>.
///
/// There is one topic. There is no retry topic, no invalid record topic and no dead letter
/// topic, because nothing in Kafka will move a record to one for you. If you want any of those,
/// you write the producer, the consumer, the scheduler and the state yourself.
///
/// **Three partitions**, because a partition is the unit of both ordering and parallelism:
/// one consumer in a group holds a partition at a time, records within a partition are ordered,
/// and records in different partitions are not ordered relative to each other at all.
/// </summary>
public static class Stream
{
    public const string BootstrapServers = "localhost:9092";
    public const string ConsumerGroup = "practical-messaging-streams";

    /// <summary>
    /// The price consumer reads a different topic for a different reason, so it gets a group of
    /// its own. Two consumers in one group would be told to share the partitions of everything
    /// the group subscribes to, which is not what either of them wants -- and the offsets of
    /// streams.OrderPlaced and streams.PriceChanged have nothing to do with each other.
    ///
    /// **A consumer group is a unit of work-sharing, not a name for your application.**
    /// </summary>
    public const string PriceConsumerGroup = "practical-messaging-prices";

    public const int Partitions = 3;

    public static string TopicFor<T>() => "streams." + typeof(T).Name;

    /// <summary>
    /// Create the topic if it is not there.
    ///
    /// Both the producer and the consumer call this, so it does not matter which you start
    /// first. (Compare RabbitMQ, where only the consumer declares the queue -- so anything
    /// published before the consumer's first ever run went nowhere. Kafka's topic is shared
    /// state that either end can create, which is a small but real difference in how the two
    /// feel to operate.)
    ///
    /// It is here rather than left to the broker's auto-create so that the partition count is
    /// ours to choose, and so the exercises do not depend on a broker setting.
    /// </summary>
    public static async Task EnsureTopicExists(string topic, string bootstrapServers = BootstrapServers)
    {
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync([
                new TopicSpecification { Name = topic, NumPartitions = Partitions, ReplicationFactor = 1 }
            ]);
            Console.WriteLine($"Created topic {topic} with {Partitions} partitions");
        }
        catch (CreateTopicsException e) when (e.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Somebody got there first, which is the normal case after the first run.
        }
    }
}
