using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class RequestReplyChannelResponder<TResponse> : IDisposable where TResponse: IAmAResponse
    {
        private readonly Func<TResponse, string> _messageSerializer;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RequestReplyChannelResponder(
            Func<TResponse, string> messageSerializer,
            string hostName = "localhost")
        {
            _messageSerializer = messageSerializer;
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            //it would make sense to pool the connections, so tht this and the consumer use the same one
            //with multiplexed channels, but we don't implement that here.
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                AutomaticRecoveryEnabled = true
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
 
        }

        public void Respond(string replyQueuename, TResponse response)
        {
            try
            {
                Console.WriteLine("Responding on queue {0} to message with correlation id {1}", 
                    replyQueuename, response.CorrelationId.ToString());
                
                
                /*
                 * TODO: crate basic properites via the channel
                 * Set the correlation id
                 * serialize the message
                 * Turn it into UTF8
                 * Publish th othe default exchange hint: "" where routing key = queue name
                 * 
                 */
               
                Console.WriteLine("Responded on queue {0} at {1}", replyQueuename, DateTime.UtcNow);
     
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void ReleaseUnmanagedResources()
        {
            _channel.Close();
            _connection.Close();
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RequestReplyChannelResponder()
        {
            ReleaseUnmanagedResources();
        }
    }
}