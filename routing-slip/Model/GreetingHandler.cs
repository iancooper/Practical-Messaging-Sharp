using System;
using SimpleMessaging;

namespace Model
{
    public class GreetingHandler : IAmAHandler<Greeting>
    {
        public void Handle(Greeting message)
        {
            if (message != null)
                Console.WriteLine(message.Salutation + " " + message.Recipient);
        }
    }
}