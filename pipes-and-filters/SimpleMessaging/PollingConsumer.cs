using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMessaging
{
    public class PollingConsumer<T> where T: IAmAMessage
    {
        private readonly IAmAHandler<T> _messageHandler;
        private readonly Func<string, T> _messageSerializer;
        private readonly string _hostName;

        public PollingConsumer(IAmAHandler<T> messageHandler, Func<string, T> messageSerializer, string hostName = "localhost")
        {
            _messageHandler = messageHandler;
            _messageSerializer = messageSerializer;
            _hostName = hostName;
        }
        
        public Task Run(CancellationToken ct)
        {
            var task = Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    using (var channel = new DataTypeChannelConsumer<T>(_messageSerializer, _hostName))
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