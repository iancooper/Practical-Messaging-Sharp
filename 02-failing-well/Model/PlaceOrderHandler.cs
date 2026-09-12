using SimpleMessaging;

namespace Model;

/// <summary>
/// Application code, and this is what it should look like: a domain type in, a return or a
/// throw out. No delivery, no headers, no acknowledgement, no broker.
///
/// A test can call this. So can an HTTP endpoint. That is a consequence of the separation
/// rather than the reason for it, but it is a good smoke alarm: if you cannot call your
/// handler from a test without a broker running, the mapper has not finished its job.
/// </summary>
public class PlaceOrderHandler(Catalogue catalogue) : IAmAHandler<PlaceOrder>
{
    public async Task Handle(PlaceOrder order)
    {
        var price = await catalogue.PriceOf(order.Sku);
        var total = price * order.Quantity;

        Console.WriteLine($"  placed order {order.Id}: {order.Quantity} x {order.Sku} for {total:C} (customer {order.CustomerId})");
    }
}
