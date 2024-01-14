using Confluent.Kafka;

namespace SimpleEventing;

public class DataTypeChannelProducer<T> : IDisposable where T: IAmAMessage
{
    private readonly Func<T, string> _messageSerializer;
    private readonly IProducer<string,string> _producer;
    private readonly string _topic;

    public DataTypeChannelProducer(Func<T, string> messageSerializer, string bootStrapServer = "localhost:9092")
    {
        _messageSerializer = messageSerializer;
        _producer = new ProducerBuilder<string, string>(
            new ProducerConfig
            {
                BootstrapServers = bootStrapServer,
            }
            ).Build();
        
        _topic = "Pub-Sub-Stream-" + typeof(T).FullName;
    }
    
    public int Flush(TimeSpan fromSeconds)
    {
        return _producer.Flush(fromSeconds);
    }
    
    public void Send(T message)
    {
        var body = _messageSerializer(message);
        _producer.Produce(_topic, new Message<string, string> { Key = message.Id, Value = body });
    }
    
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }
    
    ~DataTypeChannelProducer()
    {
        ReleaseUnmanagedResources();
    }

    private void ReleaseUnmanagedResources()
    {
        _producer.Dispose();
    }


}