using Model;
using SimpleMessaging;

// The producer, and the source of every failure you are asked to survive.
//
//   dotnet run                  a good order
//   dotnet run -- flaky         TRANSIENT: the lookup fails twice, then works
//   dotnet run -- poison        PERMANENT: a SKU that is not in the catalogue, ever
//   dotnet run -- unmappable    INVALID:   a body that is not a PlaceOrder at all
//   dotnet run -- slow          an order whose lookup takes 30 seconds
//   dotnet run -- burst 20      twenty good orders
//
// Three of those are three different failures. They should not all end up in the same place.

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "good";

await using var producer = await DataTypeChannelProducer<PlaceOrder>.CreateAsync(PlaceOrder.Serialize);

switch (command)
{
    case "good":
        await Publish(PlaceOrder.For("WIDGET-1"));
        break;

    case "flaky":
        // The order is fine. The catalogue is having a bad minute and will recover.
        await Publish(PlaceOrder.For("FLAKY-1"));
        break;

    case "poison":
        // Well-formed, maps perfectly, and the handler will throw on it every single time.
        await Publish(PlaceOrder.For("NOPE-404"));
        break;

    case "unmappable":
        // Valid JSON, wrong shape. No number of retries will make this a PlaceOrder.
        const string body = """{"Id":"c0ffee","ProductCode":"WIDGET-1","Qty":1}""";
        await producer.SendRaw(body);
        Console.WriteLine($"Sent an unmappable body: {body}");
        break;

    case "slow":
        await Publish(PlaceOrder.For("GIZMO-SLOW"));
        break;

    case "burst":
        var count = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 20;
        for (var i = 0; i < count; i++)
            await Publish(PlaceOrder.For("WIDGET-1", quantity: i + 1));
        Console.WriteLine($"Sent {count} orders");
        break;

    default:
        Console.Error.WriteLine($"Unknown command '{command}'. Try: good, flaky, poison, unmappable, slow, burst");
        return 1;
}

return 0;

async Task Publish(PlaceOrder order)
{
    await producer.Send(order);
    Console.WriteLine($"Sent order {order.Id}: {order.Quantity} x {order.Sku}");
}
