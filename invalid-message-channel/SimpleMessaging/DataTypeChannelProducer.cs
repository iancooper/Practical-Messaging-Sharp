using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class BadDataTypeChannelProducer<TActual, TIntended> : IDisposable where TActual: IAmAMessage where TIntended: IAmAMessage
    {
        private readonly Func<TActual, string> _messageSerializer;
        private readonly string _routingKey;
        private const string ExchangeName = "practical-messaging-imq";
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
        /// We have split producer and consumer, as they need separate serialization/de-serialization of the message
        /// We are disposable so that we can be used within a using statement; connections
        /// are unmanaged resources and we want to remember to close them.
        /// We inject the serializer to use with this type, so we can read and write the type to the body
        /// We are following an RAI pattern here: Resource Acquisition is Initialization
        /// </summary>
        /// <param name="messageSerializer">Needs to take a message of type T and convert to a string</param>
        /// <param name="hostName">localhost if not otherwise specified</param>
        public BadDataTypeChannelProducer(Func<TActual, string> messageSerializer, string hostName = "localhost")
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
            _routingKey = "Invalid-Message-Channel." + typeof(TIntended).FullName;
            var queueName = _routingKey;

            var invalidRoutingKey = "invalid." + _routingKey;
            var invalidMessageQueueName = invalidRoutingKey;
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            
            //TODO create an argument dictionary, that has arguments for the invalid message exchange and routing key
           
            //TODO: Create our consumer queue, but add the arguments that hook up the invalid message queue (tip might be calle deal letter in RMQ docs)
            _channel.QueueBind(queue:queueName, exchange: ExchangeName, routingKey: _routingKey);
            
            //declare a queue for invalid messages off an invalid message exchange
            //messages that we nack without requeue will go here
            // TODO; Declare an invalid message queue exchange, direct and durable
            // TODO: declare an invalid message queue, durable
            // TODO: bind the queue to the exchange
    }

        /// <summary>
        /// Send a message over the channel
        /// Uses the shared routing key to ensure the sender and receiver match up
        /// </summary>
        /// <param name="message"></param>
        public void Send(TActual message)
        {
            var body = Encoding.UTF8.GetBytes(_messageSerializer(message));
            _channel.BasicPublish(exchange: ExchangeName, routingKey: _routingKey, basicProperties: null, body: body);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~BadDataTypeChannelProducer()
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