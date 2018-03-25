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
                if (message != null)
                    Console.WriteLine("Received message {0}", message);
                else
                   Console.WriteLine("Did not receive message"); 
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}