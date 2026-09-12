using System.Text;
using RabbitMQ.Client;
using SimpleMessaging;

namespace Model;

/// <summary>
/// Application code. What the business actually wanted: price the order and place it.
///
/// ---------------------------------------------------------------------------------------
///  THIS HANDLER IS PART OF THE EXERCISE. See PROBE.md.
///
///  Ask yourself one question before you read any further: how much of this method is
///  about placing an order?
/// ---------------------------------------------------------------------------------------
/// </summary>
public class PlaceOrderHandler(Catalogue catalogue) : IAmAHandler<PlaceOrder>
{
    public async Task Handle(BasicGetResult delivery)
    {
        var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
        var order = PlaceOrder.Deserialize(body);

        var price = await catalogue.PriceOf(order.Sku);
        var total = price * order.Quantity;

        Console.WriteLine($"  placed order {order.Id}: {order.Quantity} x {order.Sku} for {total:C} (customer {order.CustomerId})");
    }
}
