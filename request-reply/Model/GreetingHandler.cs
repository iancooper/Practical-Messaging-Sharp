using System;
using SimpleMessaging;

namespace Model
{
    public class GreetingHandler : IAmAHandler<Greeting, GreetingResponse>
    {
        public GreetingResponse Handle(Greeting message)
        {
            if (message != null)
            {
                Console.WriteLine("Received greeting with Correlation Id {0} and salutation {1}",  
                    message.CorrelationId, message.Salutation);
                
                return new GreetingResponse
                {
                    CorrelationId = message.CorrelationId,
                    Result = $"Received Greeting {message.Salutation}"
                };
            }

            return null;
        }
    }
}