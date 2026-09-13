using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleMessaging;

namespace Model;

/// <summary>
/// The ECST event: the catalogue service, which owns SKUs and prices, says what one costs now.
///
/// **It is a snapshot, not a delta**, and that is the decision the whole exercise turns on.
/// "WIDGET-1 is now 11.99" can be applied twice with no harm; "WIDGET-1 went up by 2.00" cannot.
/// Probe D is where that stops being a matter of taste -- see 04-lookup/README.md, step 1.
///
/// <c>ChangedAt</c> is here for two reasons and both are probes. Probe A subtracts it from the
/// moment the consumer applies the record, and that difference is your staleness. Probe C asks
/// how old your local copy is, and a copy that does not carry a date cannot answer.
/// </summary>
public record PriceChanged : IAmAMessage
{
    public required string Id { get; init; }
    public required string Sku { get; init; }
    public required decimal Price { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }

    private static readonly JsonSerializerOptions Strict = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Serialize(PriceChanged @event) => JsonSerializer.Serialize(@event, Strict);

    public static PriceChanged Deserialize(string body) =>
        JsonSerializer.Deserialize<PriceChanged>(body, Strict)
        ?? throw new JsonException("Record deserialized to null");

    public static PriceChanged For(string sku, decimal price) => new()
    {
        Id        = Guid.NewGuid().ToString(),
        Sku       = sku,
        Price     = price,
        ChangedAt = DateTimeOffset.UtcNow
    };
}
