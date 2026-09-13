using Model;
using SimpleEventing;

// Stands in for the catalogue service: it owns prices, and it tells the world when one changes.
//
//   dotnet run --project PriceSeeder -- seed                 a starting price for every SKU
//   dotnet run --project PriceSeeder -- set WIDGET-1 11.99   change one, on demand
//
// It publishes and exits. It keeps no state, because the stream is the state -- which is the
// whole argument for ECST, and the reason a new price consumer with an empty copy can catch up
// by reading the log from the beginning.

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "seed";

// The two SKUs that survived exercise 4. GIZMO-SLOW and FLAKY-1 are gone: they were failures of
// an on-demand lookup, and there is no longer a lookup to fail. See Model/Catalogue.cs.
var starting = new (string Sku, decimal Price)[]
{
    ("WIDGET-1", 9.99m),
    ("GIZMO-2", 24.50m),
};

using var producer = await EventStreamProducer<PriceChanged>.CreateAsync(
    serializer: PriceChanged.Serialize,
    // Keyed by SKU, so two changes to one price stay in order. Key it by the event's own id
    // instead and they land on different partitions, and yesterday's price can be applied on
    // top of today's -- which is a bug you will not see until the day it costs money.
    partitionKey: @event => @event.Sku);

switch (command)
{
    case "seed":
        foreach (var (sku, price) in starting)
            await Publish(PriceChanged.For(sku, price));
        Console.WriteLine($"Seeded {starting.Length} prices.");
        break;

    case "set":
        if (args.Length < 3 || !decimal.TryParse(args[2], out var newPrice))
        {
            Console.Error.WriteLine("Usage: set <SKU> <PRICE>   e.g. set WIDGET-1 11.99");
            return 1;
        }
        await Publish(PriceChanged.For(args[1], newPrice));
        break;

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Try: seed, set");
        return 1;
}

return 0;

async Task Publish(PriceChanged @event)
{
    await producer.Send(@event);
    Console.WriteLine($"Published {@event.Sku} = {@event.Price:C} at {@event.ChangedAt:HH:mm:ss.fff}");
}
