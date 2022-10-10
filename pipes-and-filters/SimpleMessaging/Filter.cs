using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMessaging
{
    public class Filter<TIn, TOut> where TIn: IAmAMessage where TOut: IAmAMessage
    {
        private readonly IAmAnOperation<TIn, TOut> _operation;
        private readonly Func<string, TIn> _messageDeserializer;
        private readonly Func<TOut, string> _messageSerializer;
        private readonly string _hostName;

        public Filter(IAmAnOperation<TIn, TOut> operation, Func<string, TIn> messageDeserializer, Func<TOut, string> messasgeSerializer, string hostName = "localhost")
        {
            _operation = operation;
            _messageDeserializer = messageDeserializer;
            _messageSerializer = messasgeSerializer;
            _hostName = hostName;
        }
       
        /// <summary>
        /// In essence a filter step takes an input channel, reads the message, performs an operation on it, and then sends it to an output channel
        /// It is worth noting that the filter should read one message, process, then re-post to be considered pipes-and-filters over
        /// batch processing. Within pipes and filters we can therefore use competing consumers to speed the operation by parallelizing a step.
        /// If ordering is important we may need to use scatter-gather to re-assemble the order
        /// Note that we don't just use the PollingConsumer as this is a DataSink i.e. it does not pass messages any further (for information
        /// the producer step is the Data Source).
        /// In theory we could add many filter steps of this form. The key to making this work is routing. By using a datatype channel with
        /// the queue name set from the type we obscure this slightly as we rely on listening to the correct type of message, and using the
        /// message type as the routing key to ensure this all hooks together.
        /// But a key issue for pipes-and-filters is that changing the route, requires modifying sender and existing receiver to change the
        /// routing keys we use.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task Run(CancellationToken ct)
        {
            var task = Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    using (var inPipe = new DataTypeChannelConsumer<TIn>(_messageDeserializer, _hostName))
                    {
                        while (true)
                        {
                            var inMessage = inPipe.Receive();
                            if (inMessage != null)
                            {
                                TOut outMessage = _operation.Execute(inMessage);
                                using (var outPipe = new DataTypeChannelProducer<TOut>(_messageSerializer, _hostName))
                                {
                                    outPipe.Send(outMessage);
                                }
                            }
                            else
                            {
                                Task.Delay(1000, ct).Wait(ct); //yield
                            }
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                }, ct
            );
            return task;
        }
    }
}