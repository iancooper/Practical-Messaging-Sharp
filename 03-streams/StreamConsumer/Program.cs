using Model;
using SimpleEventing;

// Follows the OrderPlaced stream and counts what it sees.
//
//   dotnet run --project StreamConsumer
//
// The count is the point. An order placed once should appear once.

Console.Out.Flush();
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

Console.WriteLine($"StreamConsumer starting. PID {Environment.ProcessId}");

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

// How many times have we seen an event for each order? Anything above one is a duplicate,
// and duplicates are what exercise 3 is about.
var seen = new Dictionary<string, int>();

using var consumer = await EventStreamConsumer<OrderPlaced>.CreateAsync(
    mapper: OrderPlaced.Deserialize,
    handler: @event =>
    {
        seen[@event.OrderId] = seen.GetValueOrDefault(@event.OrderId) + 1;
        var count = seen[@event.OrderId];
        var flag = count > 1 ? $"  <-- DUPLICATE, seen {count} times" : "";

        Console.WriteLine($"  order {@event.OrderId}: {@event.Quantity} x {@event.Sku} for {@event.Total:C}{flag}");
        return Task.CompletedTask;
    });

await consumer.Run(stopping.Token);

Console.WriteLine();
Console.WriteLine($"Distinct orders seen: {seen.Count}. Events read: {seen.Values.Sum()}.");
foreach (var (orderId, count) in seen.Where(s => s.Value > 1))
    Console.WriteLine($"  {orderId} arrived {count} times");
