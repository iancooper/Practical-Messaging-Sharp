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
                            var inMessage = inPipe.Receive();
                            if (inMessage != null)
                            {
                                T outMessage = _operation.Execute(inMessage);
                                outMessage.Steps[inMessage.CurrentStep].Completed = true;
                                
                                int nextStepNo = inMessage.CurrentStep + 1;
                                if (inMessage.Steps.ContainsKey(nextStepNo))
                                {
                                    var nextStep = inMessage.Steps[nextStepNo];
                                    var outRoutingStep = nextStep.RoutingKey;
                                    
                                    outMessage.CurrentStep = nextStepNo;
                                    using (var outPipe = new DataTypeChannelProducer<T>(outRoutingStep, _messageSerializer, _hostName))
                                    {
                                        outPipe.Send(outMessage);
                                    }
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
