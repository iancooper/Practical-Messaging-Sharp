using System.Text.Json;
using SimpleMessaging;

namespace Model;

/// <summary>
/// The Translate stage for this channel: a body becomes a <see cref="PlaceOrder"/>, or it
/// does not and we say so clearly.
///
/// Note what it does *not* do: it does not log, it does not decide anything, and it does not
/// know a broker exists. It converts, or it throws.
/// </summary>
public class PlaceOrderMapper : IAmAMessageMapper<PlaceOrder>
{
    public PlaceOrder MapToRequest(string body)
    {
        try
        {
            return PlaceOrder.Deserialize(body);
        }
        catch (JsonException e)
        {
            // Translate the serializer's complaint into the gateway's vocabulary. The pump
            // should not have to know we chose JSON.
            throw new UnmappableMessageException($"Body is not a PlaceOrder: {e.Message}", e);
        }
    }
}
