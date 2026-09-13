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

// Say how old the copy is, once, at startup. It is the only line in the system that knows --
// and watch what Probe C makes of that. Knowing at startup is not the same as noticing, and a
// receiver that prices ten thousand orders from a four-day-old copy will say this once.
var newest = await prices.NewestChangedAt();
var age = newest is null ? "empty" : $"newest change {Age(newest.Value)} old";
Console.WriteLine($"Local copy is {SqlitePriceStore.DefaultPath}, holding {await prices.Count()} prices -- {age}.");

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

// How old, in words, without saying "1 minutes". A copy's age is the one number that tells a
// current local copy from a stale one, so it is worth printing in a shape a human reads.
static string Age(DateTimeOffset when)
{
    var d = DateTimeOffset.UtcNow - when;
    return d.TotalMinutes < 1 ? $"{d.TotalSeconds:0}s"
         : d.TotalHours   < 1 ? $"{d.TotalMinutes:0}m"
         : d.TotalDays    < 1 ? $"{d.TotalHours:0}h"
                              : $"{d.TotalDays:0}d";
}
