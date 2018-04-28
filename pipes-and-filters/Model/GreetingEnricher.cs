using SimpleMessaging;

namespace Model
{
    public class GreetingEnricher : IAmAnOperation<Greeting, EnrichedGreeting>
    {
        public EnrichedGreeting Execute(Greeting message)
        {
            var enriched = new EnrichedGreeting();
            enriched.Salutation = message.Salutation;
            enriched.Recipient = "Clarissa Harlowe";
            return enriched;
        }
    }
}