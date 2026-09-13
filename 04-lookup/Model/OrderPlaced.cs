using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleMessaging;

namespace Model;

/// <summary>
/// An Event Message: this happened. Many readers may care, none of them may reply, and it is
/// a statement about the past rather than a request.
///
/// Contrast <see cref="PlaceOrder"/>, which was a Command: one recipient, allowed to fail.
/// Same system, ten milliseconds apart, and the difference in intent is the whole reason one
/// goes on a queue and the other on a stream.
/// </summary>
public record OrderPlaced : IAmAMessage
{
    public required string Id { get; init; }
    public required string OrderId { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal Total { get; init; }
    public required DateTimeOffset PlacedAt { get; init; }

    private static readonly JsonSerializerOptions Strict = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(OrderPlaced @event) => JsonSerializer.Serialize(@event, Strict);

    public static OrderPlaced Deserialize(string body) =>
        JsonSerializer.Deserialize<OrderPlaced>(body, Strict)
        ?? throw new JsonException("Record deserialized to null");
}
