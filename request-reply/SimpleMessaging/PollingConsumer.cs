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
                            var request = channel.Receive();
                            if ( request != null)
                            {

                                var response = _messageHandler.Handle(request);
                                
                                var responder = new RequestReplyChannelResponder<TResponse>(
                                    _messageSerializer 
                                    );
                               
                                responder.Respond(request.ReplyTo, response);
                            }
                            else
                               Console.WriteLine("Did not receive message"); 
                                        
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