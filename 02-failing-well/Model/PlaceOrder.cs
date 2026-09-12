using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleMessaging;

namespace Model;

/// <summary>
/// A Command Message: go and do this. One recipient, and it is allowed to fail.
///
/// Every member is required and unmapped members are disallowed, so a body that is not
/// exactly this shape will not deserialize. That is deliberate -- you need a message the
/// receiver cannot understand, and "nearly the right JSON" is the realistic version of one.
/// </summary>
public record PlaceOrder : IAmAMessage
{
    public required string Id { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required string CustomerId { get; init; }

    private static readonly JsonSerializerOptions Strict = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(PlaceOrder order) => JsonSerializer.Serialize(order, Strict);

    /// <summary>Throws <see cref="JsonException"/> if the body is not a PlaceOrder.</summary>
    public static PlaceOrder Deserialize(string body) =>
        JsonSerializer.Deserialize<PlaceOrder>(body, Strict)
        ?? throw new JsonException("Body deserialized to null");

    public static PlaceOrder For(string sku, int quantity = 1, string customerId = "CUST-001") =>
        new() { Id = Guid.NewGuid().ToString(), Sku = sku, Quantity = quantity, CustomerId = customerId };
}
