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
            using (var channel = new RequestReplyChannelProducer<Greeting, GreetingResponse>(
                (greeting) => JsonConvert.SerializeObject(greeting),
                (body) => JsonConvert.DeserializeObject<GreetingResponse>(body),
                "localhost"
                )
            )
            {
                var greeting = new Greeting();
                greeting.Salutation = "Hello World!";
                var response = channel.Call(greeting, 5000);
                Console.WriteLine("Sent message Greeting {0} Correlation Id {1}", greeting.Salutation, greeting.CorrelationId);
                if (response != null)
                {
                    Console.WriteLine("Received Message {0} Correlation Id {1} at {2}", 
                        response.Result, response.CorrelationId, DateTime.UtcNow);
                }
                else
                {
                    Console.WriteLine("Did not receive a response");
                }
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}