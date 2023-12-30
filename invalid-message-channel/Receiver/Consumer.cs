using System;
using Model;
using Newtonsoft.Json;
using SimpleMessaging;

namespace Sender
{
    class Consumer
    {
        static void Main(string[] args)
        {
            /* We want to force an error on a missing property - you probably don't want this approach in production
             code as it prevents versioning, but it helps demo invalid messages by creating an error*/
            using (var channel = new DataTypeChannelConsumer<Greeting>(
                messageBody => JsonConvert.DeserializeObject<Greeting>(messageBody, new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error
                }))
            )
            {
                var greeting = channel.Receive();
                if (greeting != null)
                    Console.WriteLine("Received message {0}", greeting.Salutation);
                else
                    Console.WriteLine("Did not receive message"); 
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}