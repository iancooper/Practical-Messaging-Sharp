using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class RequestReplyChannelConsumer<T> : IDisposable where T: IAmAMessage
    {
        private readonly Func<string, T> _messageDeserializer;
        private readonly string _queueName;
        private const string ExchangeName = "practical-messaging-request-reply";
        private const string InvalidMessageExchangeName = "practical-messaging-invalid";
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
        /// We support an invalid message queue, for items that we cannot deserialize into the datatype on the channel
        /// correctly. This
        /// </summary>
        /// <param name="messageDeserializer">Takes the message body and turns it into an instance of type T</param>
        /// <param name="hostName"></param>
        public RequestReplyChannelConsumer(Func<string, T> messageDeserializer, string hostName = "localhost")
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
            var routingKey = "Request-Reply." + typeof(T).FullName;
            _queueName = routingKey;

            var invalidRoutingKey = "invalid." + routingKey;
            var invalidMessageQueueName = invalidRoutingKey;
            
            //Make the exchange durable, so that we can keep our messages/queues between restarts
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
            var arguments = new Dictionary<string, object>()
            {
                {"x-dead-letter-exchange", InvalidMessageExchangeName},
                {"x-dead-letter-routing-key", invalidRoutingKey}
            };
            
            //if we are going to have persistent messages, it mostly makes sense to have a durable queue, to survive
            //restarts, or client failures
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
            _channel.QueueBind(queue: _queueName, exchange: ExchangeName, routingKey: routingKey);
            
            //declare a queue for invalid messages off an invalid message exchange
            //messages that we nack without requeue will go here
            _channel.ExchangeDeclare(InvalidMessageExchangeName, ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(queue: invalidMessageQueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue:invalidMessageQueueName, exchange:InvalidMessageExchangeName, routingKey:invalidRoutingKey);

        }
        

        /// <summary>
        /// Receive a message from the queue
        /// The queue should have received all message published because we create it in the constructor, so the
        /// producer will create as well as the consumer making the ordering unimportant
        /// </summary>
        /// <returns></returns>
        public T Receive()
        {
            var result = _channel.BasicGet(_queueName, autoAck: false);
            if (result != null)
                try
                {
                    /*
                     * TODO
                     * deserialize the message
                     * Acknowledge the message (use the delivery tag)
                     * set the reply to property of the message, from the result properties
                     * return the message
                     */
                    
                   //T message =
                   return message;
                }
                catch (JsonSerializationException e)
                {
                    Console.WriteLine($"Error processing the incoming message {e}");
                    //put format errors onto the invalid message queue
                    _channel.BasicNack(deliveryTag:result.DeliveryTag, multiple: false, requeue:false);
                }
            
            return default(T) ;
        }   

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RequestReplyChannelConsumer()
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