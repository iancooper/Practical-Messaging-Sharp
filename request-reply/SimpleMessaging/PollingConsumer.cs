using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SimpleMessaging
{
    public class PollingConsumer<T, TResponse> where T: IAmAMessage where TResponse: IAmAResponse
    {
        private readonly IAmAHandler<T, TResponse> _messageHandler;
        private readonly Func<string, T> _messageDeserializer;
        private readonly Func<TResponse, string> _messageSerializer;
        private readonly string _hostName;

        public PollingConsumer(
            IAmAHandler<T, TResponse> messageHandler, 
            Func<string, T> messageDeserializer, 
            Func<TResponse, string> messageSerializer, 
            string hostName = "localhost")
        {
            _messageHandler = messageHandler;
            _messageDeserializer = messageDeserializer;
            _messageSerializer = messageSerializer;
            _hostName = hostName;
        }
        
        public Task Run(CancellationToken ct)
        {
            var task = Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    using (var channel = new RequestReplyChannelConsumer<T>(_messageDeserializer, _hostName))
                    {
                        while (true)
                        {
                            /*
                             * TODO: receive a request on the channel
                             * if the request is not null then
                             *     handle the message
                             *     create a RequestReplyChannelResponder
                             *     respond to the request, with the response from the handler
                             */
                            
                            
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