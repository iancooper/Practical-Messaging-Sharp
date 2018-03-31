using SimpleMessaging;

namespace Model
{
    public class BadGreeting: IAmAMessage
    {
        public int GreetingNumber { get; set; } = 42;
    }
}