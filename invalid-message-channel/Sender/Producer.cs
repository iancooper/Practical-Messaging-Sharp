using System;
using Model;
using Newtonsoft.Json;
using SimpleMessaging;

namespace Sender
{
    class Producer
    {
        static void Main(string[] args)
        {
            using (var channel = new BadDataTypeChannelProducer<BadGreeting, Greeting>((bad_greeting) => JsonConvert.SerializeObject(bad_greeting)))
            {
                var greeting = new BadGreeting();
                greeting.GreetingNumber = 3;
                channel.Send(greeting);
                Console.WriteLine("Sent message {0}", greeting.GreetingNumber);
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}