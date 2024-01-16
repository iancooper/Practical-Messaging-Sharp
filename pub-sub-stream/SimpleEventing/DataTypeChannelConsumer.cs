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
        
        //TODO: Create a ConsumerConfig file to configure Kafka. You will need to set:
        // BootstrapServers
        // GroupId
        // AutoOffsetReset (earliest)
        // EnableAutoCommit (true)
        // EnableAutoOffsetStore (false)
        
        //TODO: Build a Consumer using the ConsumerConfig above
        // SetErrorHandler to write to the console
        // SetLogHandler to write to the console
        // SetPartitionsRevokedHandler to store the offset for each partition
        
        //TODO: Subscribe to the topic "Pub-Sub-Stream-" + typeof(T).FullName
        
    }
    
    public async Task Receive(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                //TODO: Consume a message from Kafka
                
                //TODO: Translate the message using the _translator function
    
                //TODO: Handle the message using the _handler function

                //TODO: Store the offset for the partition in the background thread
                //We don't want to commit unless we have successfully handled the message
                //_consumer.Commit(consumeResult); would commit manually, but with EnableAutoOffsetStore disabled,
                //we can instead just manually store "done" offsets for a background thread to commit
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