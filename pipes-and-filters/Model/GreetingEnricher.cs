using System;
using SimpleMessaging;

namespace Model
{
    public class GreetingEnricher : IAmAnOperation<Greeting, EnrichedGreeting>
    {
        public EnrichedGreeting Execute(Greeting message)
        {
            Console.WriteLine($"Received greeting {message.Salutation}");
            var enriched = new EnrichedGreeting();
            enriched.Salutation = message.Salutation;
            enriched.Recipient = "Clarissa Harlowe";
            Console.WriteLine($"Enriched with {enriched.Recipient}");
            return enriched;
        }
    }
}