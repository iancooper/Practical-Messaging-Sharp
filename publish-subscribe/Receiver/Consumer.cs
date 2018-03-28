using System;
using System.Threading.Tasks;
using SimpleMessaging;

namespace Sender
{
    class Consumer
    {
        static void Main(string[] args)
        {
            using (var channel = new PublishSubscribeChannel(channelType: ChannelType.Subscriber))
            {
                Console.WriteLine("Waiting for message...");
                int loop = 0;
                while (true)
                {
                    loop++;
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Enter)
                            break;
                    }

                    var message = channel.Receive();
                    if (message != null)
                        Console.WriteLine("Received message {0}", message);
                    else
                        Console.WriteLine("Did not receive message");

                    Console.WriteLine("Pausing for breath...");
                    Console.WriteLine(" Press [enter] to exit.");
                    Task.Delay(4000).Wait();
                }
            }
        }
    }
}