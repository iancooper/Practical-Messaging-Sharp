namespace Model;

public class UnknownSkuException(string sku)
    : Exception($"'{sku}' is not in the catalogue");

/// <summary>
/// Reference data the handler needs in order to do its job: what does this SKU cost?
///
/// Here it is a dictionary, because exercises 1 to 3 are not about lookups. It behaves like
/// the real thing in the two ways that matter to us: it can be slow, and it can not know.
///
/// (Exercise 4, if you get to it, replaces this with a local copy filled from a stream.)
/// </summary>
public class Catalogue
{
    /// <summary>How long a lookup takes. GIZMO-SLOW is the one that hurts.</summary>
    public static readonly TimeSpan SlowLookup = TimeSpan.FromSeconds(30);

    private static readonly Dictionary<string, decimal> Prices = new()
    {
        ["WIDGET-1"]   = 9.99m,
        ["GIZMO-2"]    = 24.50m,
        ["GIZMO-SLOW"] = 24.50m,   // in the catalogue, but the lookup crawls
    };

    public async Task<decimal> PriceOf(string sku)
    {
        if (sku == "GIZMO-SLOW")
        {
            Console.WriteLine($"  catalogue: looking up {sku} (this one takes {SlowLookup.TotalSeconds:0}s)");
            await Task.Delay(SlowLookup);
        }

        if (!Prices.TryGetValue(sku, out var price))
            throw new UnknownSkuException(sku);

        return price;
    }
}
