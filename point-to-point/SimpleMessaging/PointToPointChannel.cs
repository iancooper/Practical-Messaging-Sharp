using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class PointToPointChannel : IDisposable
    {
        private string _routingKey;
        private string _queueName;
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
        /// <param name="queueName"></param>
        /// <param name="hostName"></param>
        public PointToPointChannel(string queueName, string hostName = "localhost")
        {
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            //Because we are point to point, we are just going to use queueName for the routing key
            _routingKey = queueName;
            _queueName = queueName;
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            _channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue:_queueName, exchange: ExchangeName, routingKey: _routingKey);
        }

        /// <summary>
        /// Send a message over the channel
        /// Uses the shared routing key to ensure the sender and receiver match up
        /// Note that we set queue name to routing key so this can only have one consumer i.e. point-to-point
        /// </summary>
        /// <param name="message"></param>
        public void Send(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: ExchangeName, routingKey: _routingKey, basicProperties: null, body: body);
        }

        /// <summary>
        /// Receive a message from the queue
        /// The queue should have received all message published because we create it in the constructor, so the
        /// producer will create as well as the consumer making the ordering unimportant
        /// </summary>
        /// <returns></returns>
        public string Receive()
        {
            var result = _channel.BasicGet(_queueName, autoAck: true);
            if (result != null)
                return Encoding.UTF8.GetString(result.Body.ToArray());
            else
                return null;
        }   

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~PointToPointChannel()
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