using SimpleMessaging;

namespace Model
{
    public class Greeting : IAmAMessage
    {
        public string Salutation { get; set; } = "Hello World";
        public string ReplyTo { get; set; }
    }
}