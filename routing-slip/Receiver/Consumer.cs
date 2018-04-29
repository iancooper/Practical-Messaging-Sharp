using System;
using System.Threading;
using System.Threading.Tasks;
using Model;
using Newtonsoft.Json;
using SimpleMessaging;

namespace Sender
{
    class Consumer
    {
        static void Main(string[] args)
        {
            var consumer = new PollingConsumer<EnrichedGreeting>(
                    GlobalStepList.Receiver,
                    new GreetingHandler(), 
                    messageBody => JsonConvert.DeserializeObject<EnrichedGreeting>(messageBody)
                );

            var tokenSource = new CancellationTokenSource();

            try
            {
                Console.WriteLine("Consumer running, entering loop until signalled");
                Console.WriteLine(" Press [enter] to exit.");
                //has its own thread and will continue until signalled
                var task = consumer.Run(tokenSource.Token);
                while (true)
                {
                    //loop until we get a keyboard interrupt
                    if (Console.KeyAvailable)
                    {
                        //Note: This will deadlock with Console.WriteLine on the task thread unless we have called Writeline first
                        var key = Console.ReadKey();
                        if (key.Key == ConsoleKey.Enter)
                        {
                            //signal exit
                            tokenSource.Cancel();
                            //wait for thread to error
                            task.Wait();
                            //in theory we don't get here, because we raise an exception on cancellation, but just in case
                            break;
                        }

                        Task.Delay(3000).Wait();  // yield
                    }
                }
            }
            catch (AggregateException ae)
            {
                foreach (var v in ae.InnerExceptions)
                    Console.WriteLine(ae.Message + " " + v.Message);
            }
            finally
            {
                tokenSource.Dispose();
            }

        }
    }
}