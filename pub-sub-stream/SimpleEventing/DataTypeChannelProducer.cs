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
        
        //TODO: Create a ProducerConfig file to configure Kafka. You will need to set:
        // BootstrapServers
        
        //Create the Kafka Producer
        
        //TODO: Set the topic to "Pub-Sub-Stream-" + typeof(T).FullName
        _topic = "Pub-Sub-Stream-" + typeof(T).FullName;
    }
    
    public int Flush(TimeSpan fromSeconds)
    {
        //TODO: Flush the producer
    }
    
    public void Send(T message)
    {
        var body = _messageSerializer(message);
        //TODO: Send the message to Kafka
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