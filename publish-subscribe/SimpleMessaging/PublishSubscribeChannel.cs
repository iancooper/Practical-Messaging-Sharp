using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public enum ChannelType
    {
        Publisher,
        Subscriber
    }
    
    
    public class PublishSubscribeChannel : IDisposable
    {
        private readonly ChannelType _channelType;
        private const string ALL = "";
        private const string ExchangeName = "practical-messaging";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName;

        /// <summary>
        /// Create a new channel for sending publish-subscribe messages
        /// Under RMQ we:
        ///     1. Create a socket connection to the broker
        ///     2. Create a channel on that socket
        ///     3. Create a fanout exchange on the server for publish-subscribe messaging 
        ///     4. Create a queue to hold messages
        ///     5. Bind the queue to listen to that exchange
        ///     6. Note that the routing key is irrelevant on a fanout exchange - everyone gets everything
        /// We are disposable so that we can be used within a using statement; connections
        /// are unmanaged resources and we want to remember to close them.
        /// We are following an RAI pattern here: Resource Acquisition is Initialization
        /// We could implement seperate interfaces for publish and subscribe but we are keeping this demo code simple
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="hostName"></param>
        public PublishSubscribeChannel(ChannelType channelType, string hostName = "localhost")
        {
            _channelType = channelType;
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Fanout, durable: false);
            if (channelType == ChannelType.Subscriber)
            {
                //make the queue exclusive and autoDelete as it exists only for this subscriber; a publisher does not 
                //create as we would not use the queue so created for that client, hence the flag in the constructor
                var result = _channel.QueueDeclare(durable: false, exclusive: true, autoDelete: true, arguments: null);
                _queueName = result.QueueName;
                _channel.QueueBind(queue: _queueName, exchange: ExchangeName, routingKey: ALL);
            }
        }

        /// <summary>
        /// Send a message over the channel
        /// Because we are using a fanout exchange, every consumer on the exchange will get the message
        /// </summary>
        /// <param name="message"></param>
        public void Send(string message)
        {
            if (_channelType != ChannelType.Publisher)
                throw new InvalidOperationException("You cannot send from a consumer");
            
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: ExchangeName, routingKey:ALL, basicProperties: null, body: body);
        }

        /// <summary>
        /// Receive a message from the queue
        /// Because the publisher does not know the number of consumers, it cannot pre-declare queues, as a result
        /// we can miss messages if we are not subscribing prior to the publisher sending
        /// But, by contrast, we can have multiple instances of the receiver and they will all get their own
        /// version of the message
        /// </summary>
        /// <returns></returns>
        public string Receive()
        {
            if (_channelType != ChannelType.Subscriber)
                throw new InvalidOperationException("You cannot receive on a publisher");
            
            var result = _channel.BasicGet(_queueName, autoAck: true);
            if (result != null)
                return Encoding.UTF8.GetString(result.Body);
            else
                return null;
        }   

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~PublishSubscribeChannel()
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