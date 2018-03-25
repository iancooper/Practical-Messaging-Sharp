using System;
using SimpleMessaging;

namespace Sender
{
    class Consumer
    {
        static void Main(string[] args)
        {
            using (var channel = new PointToPointChannel("hello-p2p"))
            {
                var message = channel.Receive();
                Console.WriteLine("Received message {0}", message);
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}