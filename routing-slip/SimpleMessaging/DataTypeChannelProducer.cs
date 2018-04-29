using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class DataTypeChannelProducer<T> : IDisposable where T: IAmAMessage 
    {
        private readonly Func<T, string> _messageSerializer;
        private readonly string _routingKey;
        private const string ExchangeName = "practical-messaging-paf";
        private const string InvalidMessageExchangeName = "practical-messaging-invalid";
        private readonly IConnection _connection;
        private readonly IModel _channel;

        /// <summary>
        /// Create a new channel for sending point-to-point messages
        /// Under RMQ we:
        ///     1. Create a socket connection to the broker
        ///     2. Create a channel on that socket
        ///     3. Create a direct exchange on the server for point-to-point messaging
        /// We don't create the receiving queue - each consumer does that, and will route to our
        /// key.
        /// We have split producer and consumer, as they need seperate serialization/de-serialization of the message
        /// We are disposable so that we can be used within a using statement; connections
        /// are unmanaged resources and we want to remember to close them.
        /// We inject the serializer to use with this type, so we can read and write the type to the body
        /// We are following an RAI pattern here: Resource Acquisition is Initialization
        /// </summary>
        /// <param name="routingKey">The key of the next step in the sequence</param>
        /// <param name="messageSerializer">Needs to take a message of type T and convert to a string</param>
        /// <param name="hostName">localhost if not otherwise specified</param>
        public DataTypeChannelProducer(string routingKey, Func<T, string> messageSerializer, string hostName = "localhost")
        {
            _messageSerializer = messageSerializer;
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
             /* We choose to base the key off the type name, because we want tp publish to folks interested in this type
              We name the queue after that routing key as we are point-to-point and only expect one queue to receive
             this type of message */
            _routingKey = routingKey;
            var queueName = _routingKey;

            var invalidRoutingKey = "invalid." + _routingKey;
            var invalidMessageQueueName = invalidRoutingKey;
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
            var arguments = new Dictionary<string, object>()
            {
                {"x-dead-letter-exchange", InvalidMessageExchangeName},
                {"x-dead-letter-routing-key", invalidRoutingKey}
            };
            
            //if we are going to have persistent messages, it mostly makes sense to have a durable queue, to survive
            //restarts, or client failures
             _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
            _channel.QueueBind(queue:queueName, exchange: ExchangeName, routingKey: _routingKey);
            
            //declare a queue for invalid messages off an invalid message exchange
            //messages that we nack without requeue will go here
           _channel.ExchangeDeclare(InvalidMessageExchangeName, ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(queue: invalidMessageQueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue:invalidMessageQueueName, exchange:InvalidMessageExchangeName, routingKey:invalidRoutingKey);
    }

        /// <summary>
        /// Send a message over the channel
        /// Uses the shared routing key to ensure the sender and receiver match up
        /// </summary>
        /// <param name="message"></param>
        public void Send(T message)
        {
            var body = Encoding.UTF8.GetBytes(_messageSerializer(message));
            //In order to do guaranteed delivery, we want to use the broker's message store to hold the message, 
            //so that it will be available even if the broker restarts
            var props = _channel.CreateBasicProperties();
            props.DeliveryMode = 2; //persistent
            _channel.BasicPublish(exchange: ExchangeName, routingKey: _routingKey, basicProperties: props, body: body);
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
            _channel.Close();
            _connection.Close();
        }
    }
}