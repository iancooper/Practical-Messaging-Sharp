using SimpleMessaging;

namespace Model;

/// <summary>
/// Application code: price the order, place it, and tell the world it happened.
///
/// A command came in on a queue; a fact goes out on a stream. That is an entirely ordinary
/// shape and you have probably written it -- which is the point.
///
/// ---------------------------------------------------------------------------------------
///  Nothing in this class is wrong, and that is what makes exercise 3 worth doing. The
///  handler is clean, the domain has no broker in it, the ordering is the sensible one.
///  Read PROBE.md before you run it, and predict what a crash costs you.
/// ---------------------------------------------------------------------------------------
/// </summary>
public class PlaceOrderHandler(Catalogue catalogue, IPublishEvents<OrderPlaced> events)
    : IAmAHandler<PlaceOrder>
{
    /// <summary>
    /// How long to pause between publishing the event and returning to the pump -- which is
    /// to say, between the Kafka write and the RabbitMQ acknowledgement.
    ///
    /// In a real service that gap is microseconds wide. It is still a gap, and a service that
    /// handles a million orders will fall into it. Widening it to ten seconds does not create
    /// the problem; it just means you can aim at it.
    ///
    ///   DUAL_WRITE_WINDOW=10 dotnet run --project Receiver
    /// </summary>
    private static readonly TimeSpan Window =
        TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("DUAL_WRITE_WINDOW"), out var s) ? s : 0);

    public async Task Handle(PlaceOrder order)
    {
        var price = await catalogue.PriceOf(order.Sku);
        var total = price * order.Quantity;

        Console.WriteLine($"  placed order {order.Id}: {order.Quantity} x {order.Sku} for {total:C}");

        await events.Publish(new OrderPlaced
        {
            Id       = Guid.NewGuid().ToString(),
            OrderId  = order.Id,
            Sku      = order.Sku,
            Quantity = order.Quantity,
            Total    = total,
            PlacedAt = DateTimeOffset.UtcNow
        });

        if (Window > TimeSpan.Zero)
        {
            Console.WriteLine($"  [the event is on the stream. RabbitMQ has NOT been acked yet.]");
            Console.WriteLine($"  [you have {Window.TotalSeconds:0} seconds. kill -9 {Environment.ProcessId}]");
            await Task.Delay(Window);
        }
    }
}
