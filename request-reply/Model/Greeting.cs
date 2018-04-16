using System;
using SimpleMessaging;

namespace Model
{
    public class Greeting : IAmAMessage
    {
        public string Salutation { get; set; } = "Hello World";
        public Guid CorrelationId { get; set; } = Guid.NewGuid();
        public string ReplyTo { get; set; }
    }

    public class GreetingResponse : IAmAResponse
    {
        public Guid CorrelationId { get; set; }
        public string Result { get; set; }
    }
}