using System;
using System.Text;
using System.Threading;
using Model;
using Newtonsoft.Json;
using SimpleMessaging;

namespace Sender
{
    class Consumer
    {
        static void Main(string[] args)
        {
            Console.Title = "Practical Messaging: NotDatabype-Challel example (consumer). Press any key to stop.";
            using (var channel = new NotDataTypeChannelConsumer())
            {
                while (!Console.KeyAvailable)
                {
                    (object typeHeader, byte[] serialisedMessage) = channel.Receive();
                    if(typeHeader == null && serialisedMessage == null)
                    {
                        Thread.Sleep(1300);
                        continue;
                    }
                    object message = Deserialise(typeHeader, serialisedMessage);

                    if( message as Greeting != null)
                    {
                        Console.WriteLine("Greeting: "+ ((Greeting)message).Salutation);
                    }
                    else if (message as Parting != null)
                    {
                        Console.WriteLine("Parting: " + ((Parting)message).Salutation);
                    }
                    else
                    {
                        Console.WriteLine("Uknown message type.");
                    }

                }
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }

        private static object Deserialise(object type, byte[] message)
        {
            string typeString = Encoding.UTF8.GetString((byte[])type);
            if (typeString == typeof(Greeting).FullName)
            {
                return JsonConvert.DeserializeObject<Greeting>(Encoding.UTF8.GetString(message));
            }
            else if(typeString == typeof(Parting).FullName)
            {
                return JsonConvert.DeserializeObject<Parting>(Encoding.UTF8.GetString(message));
            }
            else
            {
                Console.WriteLine("Cannot deserialise type {0} becuase it is unknown.", typeString);
                return null;
            }
        }
    }
}