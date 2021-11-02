using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class NotDataTypeChannelConsumer : IDisposable
    {
        private const string _routingKey = "practical-messaging-not-datatype";
        private const string _queueName = "practical-messaging-not-datatype-consumer";
        private const string ExchangeName = "practical-messaging";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private bool disposedValue;

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
        public NotDataTypeChannelConsumer(string hostName = "localhost")
        {
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
                        
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            _channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue:_queueName, exchange: ExchangeName, routingKey: _routingKey);
        }

        /// <summary>
        /// <para>
        /// Receive and ack a message from the queue if one is available, else return null.
        /// </para>
        /// <para>
        /// Deserialising is done in here which creates a dependency on the model -- this class is probably doing too much!
        /// </para>
        /// <para>
        /// The queue should have received all message published because we create it in the constructor, so the
        /// producer will create as well as the consumer making the ordering unimportant.
        /// </para>
        /// </summary>
        /// <returns>The message type string (C# clas full name), and the (serialised) message body.</returns>
        public (object, byte[]) Receive()
        {
            BasicGetResult result = _channel.BasicGet(_queueName, autoAck: true);
            if (result != null)
            {
                object type = null;
                bool gotType = false;
                if (result.BasicProperties != null && result.BasicProperties.Headers != null)
                {
                    gotType = result.BasicProperties.Headers.TryGetValue("x-message-type", out type);
                }

                if( !gotType)
                {
                    string foo = Encoding.UTF8.GetString(result.Body);
                    return (null, null);
                }

                // We can't deserialise the message here because we don't depend on the Model dll.
                // We could inject a deserialisation service, as in the other examples, but for simplicity we leave it for the caller to deal with.

                return (type, result.Body);
            }
            else
            {
                return (null, null);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _channel.Close();
                    _channel.Dispose();

                    _connection.Close();
                    _connection.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~NotDataTypeChannelProducer()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}