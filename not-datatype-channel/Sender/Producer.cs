using System;
using System.Threading;
using Model;
using SimpleMessaging;

namespace Sender
{
    /// <summary>
    /// Ina a channel that is not 'data-type' we send messages in multiple different formats, e.g., because they must be processed in sequence.
    /// </summary>
    class Producer
    {
            class Doodah
        {
            public string Salutation { get; set; }
        }

        static void Main(string[] args)
        {
            Console.Title = "Practical Messaging: NotDatabype-Challel example (producer). Press any key to stop.";
            using (var channel = new NotDataTypeChannelProducer())
            {
                object msg = null;
                bool arrived = false;
                int n = 1;
                while (!Console.KeyAvailable)
                {
                    int i = n;
                    if (!arrived)
                    {
                        if (DateTime.Now.Millisecond < 359)
                        {
                            msg = new Doodah() { Salutation = "BOO! Bet you weren't expecting this: " + n.ToString() };
                        }
                        else
                        {
                            msg = new Greeting() { Salutation = "Hello " + n.ToString() };
                            arrived = true;
                        }
                    }
                    else
                    {
                        msg = new Parting() { Salutation = "Goodbye " + n.ToString() };
                        arrived = false;
                        n++;
                    }

                    channel.Send(msg);
                    Console.WriteLine("Sent message {0} of type {1}.", i, msg.GetType().Name);
                    Thread.Sleep(1300);
                }

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }
    }
}