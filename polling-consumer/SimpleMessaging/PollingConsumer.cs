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
            /*
             * TODO:
             * Create a Task that will
             *     check for cancellation
             *     create a data type channel consumer
             *     while true
             *         try go get a message
             *         dispatch that message to a handler
             *         yield for 1 second
             *         check for cancellation
             *     dispose of the channel
             *  return the task
             */
        }
    }
}