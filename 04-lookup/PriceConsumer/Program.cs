using LocalCopy;
using Model;
using SimpleEventing;

// Follows streams.PriceChanged and maintains the local copy of the catalogue's prices.
//
//   dotnet run --project PriceConsumer
//   PRICE_WRITE_WINDOW=15 dotnet run --project PriceConsumer     # for Probe D
//
// **It is a process of its own, and that is deliberate.** A thread inside the Receiver would
// have been less code; it would also have made Probe B "set a flag" instead of "kill -9 a real
// process", and the whole of exercise 4 is about what happens to the people downstream of a
// thing that stops.

Console.Out.Flush();
Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

Console.WriteLine($"PriceConsumer starting. PID {Environment.ProcessId}");

// How long to pause *between* the two writes below, so that you can aim a kill at the gap.
// In a real service the gap is microseconds wide. It is still a gap. This is the same
// instrument as DUAL_WRITE_WINDOW in exercise 3, one layer down and in your own code.
var window = TimeSpan.FromSeconds(
    int.TryParse(Environment.GetEnvironmentVariable("PRICE_WRITE_WINDOW"), out var seconds) ? seconds : 0);

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nStopping...");
    stopping.Cancel();
};

using var store = await SqlitePriceStore.OpenAsync();
using var reader = await EventStreamReader<PriceChanged>.CreateAsync(
    mapper: PriceChanged.Deserialize,
    consumerGroup: SimpleEventing.Stream.PriceConsumerGroup);

Console.WriteLine($"Following {reader.Topic} as group '{SimpleEventing.Stream.PriceConsumerGroup}'");
Console.WriteLine($"Local copy is {SqlitePriceStore.DefaultPath}, holding {await store.Count()} prices.");
if (window > TimeSpan.Zero)
    Console.WriteLine($"PRICE_WRITE_WINDOW is {window.TotalSeconds:0}s -- there is a gap between the two writes.");

try
{
    while (!stopping.IsCancellationRequested)
    {
        var record = reader.Read(stopping.Token);
        if (record is null) continue;

        var @event = record.Message;

        // ---------------------------------------------------------------------------------
        //  TWO WRITES, TWO STORES, NO TRANSACTION. **PROBE D IS THE ORDER OF THESE LINES.**
        //
        //  The price goes into SQLite. The offset goes into Kafka. Nothing on this machine
        //  can make those two happen together, which is exactly what exercise 3 showed you
        //  in the Receiver -- except that this time it is a loop you wrote, and it looks
        //  like one step.
        //
        //  As written: apply, then commit. Die in between and the record is read again on
        //  restart and applied twice, which is harmless *because PriceChanged is a snapshot*.
        //  Swap the two lines and die in between and the price is lost for ever, because
        //  Kafka will never offer it again.
        // ---------------------------------------------------------------------------------
        var appliedAt = await store.Apply(@event);

        var staleness = appliedAt - @event.ChangedAt;
        Console.WriteLine(
            $"  {@event.Sku} = {@event.Price:C}  ({record.Where})  " +
            $"published-to-applied {staleness.TotalMilliseconds:0} ms");

        await PauseInTheWindow();

        reader.Commit(record);
    }
}
catch (OperationCanceledException)
{
    // Ctrl-C. The expected ending.
}

Console.WriteLine("PriceConsumer stopped.");
return 0;

async Task PauseInTheWindow()
{
    if (window <= TimeSpan.Zero) return;

    Console.WriteLine("  [one of the two writes has happened and the other has not.]");
    Console.WriteLine($"  [you have {window.TotalSeconds:0} seconds. kill -9 {Environment.ProcessId}]");
    await Task.Delay(window, stopping.Token);
}
