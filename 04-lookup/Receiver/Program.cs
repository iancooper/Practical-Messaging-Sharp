using LocalCopy;
using Model;
using SimpleEventing;
using SimpleMessaging;

// A command in on a queue, a fact out on a stream -- and now the price comes from a local copy
// of somebody else's data rather than from a dictionary.
//
//   dotnet run --project Receiver
//   DUAL_WRITE_WINDOW=10 dotnet run --project Receiver     # exercise 3's Probe A, still here
//
// **This is the only file that knows all three things at once**: that prices live in SQLite,
// that the domain wants an IPriceStore, and that the two fit together. Composition is the
// application's job. Model declares the interface and names none of the rest of it.

Console.Out.Flush();
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

Console.WriteLine($"Receiver starting. PID {Environment.ProcessId}");

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nStopping after the current message...");
    stopping.Cancel();
};

using var publisher = new KafkaEventPublisher<OrderPlaced>(
    await EventStreamProducer<OrderPlaced>.CreateAsync(
        serializer:   OrderPlaced.Serialize,
        partitionKey: @event => @event.OrderId));

using var prices = await SqlitePriceStore.OpenAsync();
Console.WriteLine($"Local copy is {SqlitePriceStore.DefaultPath}, holding {await prices.Count()} prices.");

var pump = new MessagePump<PlaceOrder>(
    new PlaceOrderMapper(),
    // PlaceOrderHandler has not changed since exercise 3, and nothing in this line asks it to.
    new PlaceOrderHandler(new Catalogue(prices), publisher));

try
{
    await pump.Run(stopping.Token);
}
catch (OperationCanceledException)
{
    // Ctrl-C. The expected ending.
}

Console.WriteLine("Receiver stopped.");
