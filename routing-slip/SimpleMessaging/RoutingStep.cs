using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMessaging
{
    public class RoutingStep<T> where T: IAmARoutingSlip
    {
        private readonly string _thisRoutingKey;
        private readonly IAmAnOperation<T> _operation;
        private readonly Func<string, T> _messageDeserializer;
        private readonly string _routingKeyOut;
        private readonly Func<T, string> _messageSerializer;
        private readonly string _hostName;

        public RoutingStep(
            string thisRoutingKey, 
            IAmAnOperation<T> operation, 
            Func<string, T> messageDeserializer, 
            Func<T, string> messasgeSerializer, 
            string hostName = "localhost")
        {
            _thisRoutingKey = thisRoutingKey;
            _operation = operation;
            _messageDeserializer = messageDeserializer;
            _messageSerializer = messasgeSerializer;
            _hostName = hostName;
        }
       
        /// <summary>
        /// A routing slip based consumer has to be aware of the slip, because it needs to know how to forward the message
        /// on to the next step. Unlike a filter that has a pre-defined destination on the filter, the routing slip carries
        /// the next destination instead.
        /// A major difference between routing slip and pipes-and-filters is coupling; in pipes-and-filters we are coupled
        /// to the next component  but in routing slip we don't know what it is.
        /// But, as a result we can't vary the datatype, unlike pipes and filters.
        /// Up to now we have used the class name of the 'message' to route to a queue. Now though we need to use separate
        /// the two as the message and the routing key are not the same.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task Run(CancellationToken ct)
        {
            var task = Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    
                   
                    
                    using (var inPipe = new DataTypeChannelConsumer<T>(_thisRoutingKey, _messageDeserializer, _hostName))
                    {
                        while (true)
                        {
                            /* TODO
                             * receive a message from the in pipe
                             * if we get non-null message
                             *     execute the operation on it to get the out message
                             *     complete the step on te in message
                             *     increment the step counter
                             *     if there is a step for the next step counter
                             *         retrieve the routing key from the next step
                             *         set the next step on the outgoing message
                             *         create an outpipe DataTypeChannelProducer
                             *             send the message
                             *         dispose of the producer
                             *     else
                             *         yield for a second
                             * 
                             * 
                             */
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