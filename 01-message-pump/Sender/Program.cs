using Model;
using SimpleMessaging;

// The producer. It puts things on the channel for you, including things the receiver will
// not like. Every probe in PROBE.md starts with one of these.
//
//   dotnet run                  one good order
//   dotnet run -- slow          an order whose lookup takes 30 seconds
//   dotnet run -- poison        an order for a SKU that is not in the catalogue
//   dotnet run -- unmappable    a body that is not a PlaceOrder at all
//   dotnet run -- burst 20      twenty good orders, as fast as we can publish them

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "good";

await using var producer = await DataTypeChannelProducer<PlaceOrder>.CreateAsync(PlaceOrder.Serialize);

switch (command)
{
    case "good":
        await Publish(PlaceOrder.For("WIDGET-1"));
        break;

    case "slow":
        await Publish(PlaceOrder.For("GIZMO-SLOW"));
        break;

    case "poison":
        // Well-formed. Maps perfectly. The handler will throw on it every single time.
        await Publish(PlaceOrder.For("NOPE-404"));
        break;

    case "unmappable":
        // Valid JSON, wrong shape. The mapper cannot turn this into a PlaceOrder, and no
        // amount of retrying will change that.
        const string body = """{"Id":"c0ffee","ProductCode":"WIDGET-1","Qty":1}""";
        await producer.SendRaw(body);
        Console.WriteLine($"Sent an unmappable body: {body}");
        break;

    case "burst":
        var count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 20;
        for (var i = 0; i < count; i++)
            await Publish(PlaceOrder.For("WIDGET-1", quantity: i + 1));
        Console.WriteLine($"Sent {count} orders");
        break;

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Try: good, slow, poison, unmappable, burst");
        return 1;
}

return 0;

async Task Publish(PlaceOrder order)
{
    await producer.Send(order);
    Console.WriteLine($"Sent order {order.Id}: {order.Quantity} x {order.Sku}");
}
