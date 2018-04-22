using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class DataTypeChannelConsumer<T> : IDisposable where T: IAmAMessage
    {
        private readonly Func<string, T> _messageDeserializer;
        private readonly string _queueName;
        private const string ExchangeName = "practical-messaging";
        private readonly IConnection _connection;
        private readonly IModel _channel;

        /// <summary>
        /// Create a new channel for sending point-to-point messages
        /// Under RMQ we:
        ///     1. Create a socket connection to the broker
        ///     2. Create a channel on that socket
        ///     3. Create a direct exchange on the server for point-to-point messaging 
        ///     4. Create a queue to hold messages
        ///     5. Bind the queue to listen to a routing key on that exchange
        /// We are disposable so that we can be used within a using statement; connections
        /// are unmanaged resources and we want to remember to close them.
        /// We are following an RAI pattern here: Resource Acquisition is Initialization
        /// </summary>
        /// <param name="messageDeserializer">Takes the message body and turns it into an instance of type T</param>
        /// <param name="hostName"></param>
        public DataTypeChannelConsumer(Func<string, T> messageDeserializer, string hostName = "localhost")
        {
            _messageDeserializer = messageDeserializer;
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
             /* We choose to base the key off the type name, because we want tp publish to folks interested in this type
              We name the queue after that routing key as we are point-to-point and only expect one queue to receive
             this type of message */
            var routingKey = nameof(T);
            _queueName = routingKey;
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            _channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue:_queueName, exchange: ExchangeName, routingKey: routingKey);
        }

        /// <summary>
        /// Receive a message from the queue
        /// The queue should have received all message published because we create it in the constructor, so the
        /// producer will create as well as the consumer making the ordering unimportant
        /// </summary>
        /// <returns></returns>
        public T Receive()
        {
            var result = _channel.BasicGet(_queueName, autoAck: true);
            if (result != null)
                return _messageDeserializer(Encoding.UTF8.GetString(result.Body));
            else
                return default(T) ;
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



        private void ReleaseUnmanagedResources()
        {
            _channel.Close();
            _connection.Close();
        }
    }
}