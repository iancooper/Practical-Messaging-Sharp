using System;
using SimpleMessaging;

namespace Model
{
    public class GreetingHandler : IAmAHandler<EnrichedGreeting>
    {
        public void Handle(EnrichedGreeting message)
        {
            if (message != null)
                Console.WriteLine(message.Salutation + " " + message.Recipient + "" + message.Bio);
        }
    }
}