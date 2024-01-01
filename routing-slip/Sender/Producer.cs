using System;
using System.Threading.Tasks;
using Model;
using Newtonsoft.Json;
using SimpleMessaging;

namespace Sender
{
    class Producer
    {
        static void Main(string[] args)
        {
            var greeting = new Greeting();
            greeting.Steps[1] = new Step(1, GlobalStepList.Enricher);
            greeting.Steps[2] = new Step(2, GlobalStepList.Receiver);
            
 
            using (var channel = new DataTypeChannelProducer<Greeting>(
                 greeting.Steps[1].RoutingKey,
                (message) => JsonConvert.SerializeObject(message)
                )
            )
            {
                Console.WriteLine(" Press [enter] to exit.");
                int loop = 0;
                while (true)
                {
                    //loop until we get a keyboard interrupt
                    if (Console.KeyAvailable)
                    {
                        //Note: This will deadlock with Console.WriteLine on the task thread unless we have called Writeline first
                        var key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Enter)
                        {
                            break;
                        }
                    }
                    
                    greeting.Salutation = "Hello World! #" + loop;
                    channel.Send(greeting);
                    Console.WriteLine("Sent message {0}", greeting.Salutation);
                    loop++;
                    
                    if (loop % 10 == 0)
                    {
                        Console.WriteLine("Pause for breath");
                        Task.Delay(3000).Wait(); // yield
                    }
                }
            }
        }
    }
}