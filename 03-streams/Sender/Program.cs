using Model;
using SimpleEventing;
using SimpleMessaging;

// Puts things on channels, including things the other end will not like.
//
//   dotnet run                  a good order, on the queue
//   dotnet run -- burst 20      twenty good orders
//   dotnet run -- poison        a SKU that is not in the catalogue (queue-side failure)
//   dotnet run -- unmappable    a body that is not a PlaceOrder (queue-side failure)
//   dotnet run -- flaky         a lookup that fails twice then works
//   dotnet run -- slow          an order whose lookup takes 30 seconds
//   dotnet run -- bad-event     A RECORD THE STREAM CONSUMER CANNOT READ -- straight onto Kafka

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "good";

if (command == "bad-event")
{
    // Appended to the stream directly, because we need a poison *record* rather than a poison
    // message. There is no such thing as putting it on an invalid record topic for us.
    using var stream = await EventStreamProducer<OrderPlaced>.CreateAsync(OrderPlaced.Serialize, e => e.OrderId);
    const string record = """{"Id":"c0ffee","OrderRef":"not-a-field","Sku":"WIDGET-1"}""";
    Console.WriteLine("Appending a record the consumer cannot map:");
    Console.WriteLine($"  {record}");
    await stream.SendRaw(key: "poison", body: record);
    return 0;
}

await using var producer = await DataTypeChannelProducer<PlaceOrder>.CreateAsync(PlaceOrder.Serialize);

switch (command)
{
    case "good":
        await Publish(PlaceOrder.For("WIDGET-1"));
        break;

    case "flaky":
        await Publish(PlaceOrder.For("FLAKY-1"));
        break;

    case "poison":
        await Publish(PlaceOrder.For("NOPE-404"));
        break;

    case "unmappable":
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
        Console.Error.WriteLine($"Unknown command '{command}'. Try: good, flaky, poison, unmappable, slow, burst, bad-event");
        return 1;
}

return 0;

async Task Publish(PlaceOrder order)
{
    await producer.Send(order);
    Console.WriteLine($"Sent order {order.Id}: {order.Quantity} x {order.Sku}");
}
