using SimpleMessaging;

namespace Model
{
    public class EnrichedGreeting : Greeting, IAmAMessage
    {
        public string Recipient { get; set; }
        public string Bio { get; set; }
    }
}