using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMessaging
{
    public class PollingConsumer<T> where T: IAmAMessage
    {
        private readonly string _routingKey;
        private readonly IAmAHandler<T> _messageHandler;
        private readonly Func<string, T> _messageSerializer;
        private readonly string _hostName;

        public PollingConsumer(Func<string, T> messageSerializer,
            IAmAHandler<T> messageHandler,
            string routingKey,
            string hostName = "localhost")
        {
            _routingKey = routingKey;
            _messageHandler = messageHandler;
            _messageSerializer = messageSerializer;
            _hostName = hostName;
        }
        
        public Task Run(CancellationToken ct)
        {
            var task = Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    using (var channel = new DataTypeChannelConsumer<T>(_messageSerializer, _routingKey, _hostName))
                    {
                        while (true)
                        {
                            var message = channel.Receive();
                            _messageHandler.Handle(message);
                            Task.Delay(1000, ct).Wait(ct); //yield
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                }, ct
            );
            return task;
        }
    }
}