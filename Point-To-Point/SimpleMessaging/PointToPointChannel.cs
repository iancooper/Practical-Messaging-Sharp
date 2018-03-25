using System;
using System.Text;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class PointToPointChannel : IDisposable
    {
        private const string QueueName = "pm-p2p-text";
        private const string ExchangeName = "practical-messaging";
        private const string RoutingKey = "pm.p2p.hello";
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public PointToPointChannel(string queueName, string hostName = "localhost")
        {
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = "localhost" };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            _channel.QueueDeclare(queue: QueueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue:QueueName, exchange: ExchangeName, routingKey: RoutingKey);
        }

        public void Send(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: ExchangeName, routingKey: RoutingKey, basicProperties: null, body: body);
        }

        public string Receive()
        {
            var result = _channel.BasicGet(QueueName, autoAck: true);
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