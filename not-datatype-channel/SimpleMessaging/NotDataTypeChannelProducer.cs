using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace SimpleMessaging
{
    public class NotDataTypeChannelProducer : IDisposable
    {
        private const string _routingKey = "practical-messaging-not-datatype";
        private const string ExchangeName = "practical-messaging";
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private bool disposedValue;

        /// <summary>
        /// Create a new channel for sending point-to-point messages.
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
        /// <param name="messageSerializer">Needs to take a message of type T and convert to a string</param>
        /// <param name="hostName">localhost if not otherwise specified</param>
        public NotDataTypeChannelProducer(string hostName = "localhost")
        {
            //just use defaults: usr: guest pwd: guest port:5672 virtual host: /
            var factory = new ConnectionFactory() { HostName = hostName };
            factory.AutomaticRecoveryEnabled = true;
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            // Because we are point to point, we are just going to use queueName for the routing key
            // just use the routing key as the queue name; we are still point-to-point.

            // We could make this even more exciting by using an exchange of type Headers
            // which would send the message to different output queues depending by inspecting the message headers.

            var queueName = _routingKey;
            
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: false);
            _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue:queueName, exchange: ExchangeName, routingKey: _routingKey);
     }

        /// <summary>
        /// <para>
        /// Send a message over the channel.
        /// Uses the shared routing key to ensure the sender and receiver match up.
        /// </para>
        /// <para>
        /// IMPORTANT! For this non-dataype channel we add a header to describe the message format (it is just the C# type name -- which may mean that the producer and consumer are 'bound' by a mutual dependency on this type).
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        public void Send(object message)
        {
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            IBasicProperties basicProperties = _channel.CreateBasicProperties();
            basicProperties.Headers = new Dictionary<string, object>();
            basicProperties.Headers.Add("x-message-type", message.GetType().FullName);
            _channel.BasicPublish(exchange: ExchangeName, routingKey: _routingKey, basicProperties: basicProperties, body: body);
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