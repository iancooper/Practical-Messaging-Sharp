using Confluent.Kafka;

namespace SimpleEventing;

public class DataTypeChannelConsumer<T> : IDisposable where T: IAmAMessage 
{
    private readonly Func<Message<string, string>, T> _translator;
    private readonly Func<T, bool> _handler;
    private readonly IConsumer<string,string> _consumer;

    public DataTypeChannelConsumer( Func<Message<string, string>, T> translator,  Func<T, bool> handler )
    {
        _translator = translator;
        _handler = handler;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "SimpleEventing",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false
        };
        
        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
            .SetLogHandler((_, lm) => Console.WriteLine($"Facility: {lm.Facility} Level: {lm.Level} Log: {lm.Message}"))
            .SetPartitionsRevokedHandler((c, partions) => partions.ForEach(p => c.StoreOffset(p)))
            .Build();
        
        _consumer.Subscribe("Pub-Sub-Stream-" + typeof(T).FullName);
        
    }
    
    public async Task Receive(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult.IsPartitionEOF)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                
                var dataType = _translator(consumeResult.Message);

                var result = _handler(dataType);
                if (result)
                {
                    //We don't want to commit unless we have successfully handled the message
                    //_consumer.Commit(consumeResult); would commit manually, but with EnableAutoOffsetStore disabled,
                    //we can instead just manually store "done" offsets for a background thread to commit
                    _consumer.StoreOffset(consumeResult);
                }
            }
        }
        catch(ConsumeException e)
        {
           Console.WriteLine(e.Message); 
        }
        catch (OperationCanceledException)
        {
            //Pump was cancelled, exit
        }
        finally
        {
            _consumer.Close();
        }
    }
    
    
    private void ReleaseUnmanagedResources()
    {
        _consumer.Close();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~DataTypeChannelConsumer()
    {
        ReleaseUnmanagedResources();
    }
}