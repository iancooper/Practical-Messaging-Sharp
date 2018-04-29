using System;
using SimpleMessaging;

namespace Model
{
    public class GreetingEnricher : IAmAnOperation<Greeting>
    {
        public Greeting Execute(Greeting message)
        {
            Console.WriteLine($"Received greeting {message.Salutation}");
            message.Recipient = "Clarissa Harlowe";
            Console.WriteLine($"Enriched with {message.Recipient}");
            return message;
        }
    }
}