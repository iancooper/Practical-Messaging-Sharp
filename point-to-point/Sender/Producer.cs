using System;
using SimpleMessaging;

namespace Sender
{
    class Producer
    {
        static void Main(string[] args)
        {
            using (var channel = new PointToPointChannel("hello-p2p"))
            {
                string message = "Hello World!";
                channel.Send(message);
                Console.WriteLine("Sent message {0}", message);
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}