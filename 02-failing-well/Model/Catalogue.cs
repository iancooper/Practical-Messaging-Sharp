namespace Model;

/// <summary>Permanent. This SKU does not exist and asking again will not change that.</summary>
public class UnknownSkuException(string sku)
    : Exception($"'{sku}' is not in the catalogue");

/// <summary>Transient. The lookup is unwell; the order is fine. Try again shortly.</summary>
public class CatalogueUnavailableException(string sku, int attempt)
    : Exception($"catalogue is unavailable (attempt {attempt} for '{sku}')");

/// <summary>
/// Reference data the handler needs: what does this SKU cost?
///
/// It fails in the three ways a real lookup fails, and telling them apart is the exercise:
///
///   WIDGET-1, GIZMO-2   fine
///   GIZMO-SLOW          in the catalogue, but the lookup takes 30 seconds
///   FLAKY-1             fails twice, then works -- a service that was restarting
///   anything else       not in the catalogue, and never will be
/// </summary>
public class Catalogue
{
    public static readonly TimeSpan SlowLookup = TimeSpan.FromSeconds(30);

    /// <summary>How many times FLAKY-1 fails before it starts working.</summary>
    public const int FlakyFailures = 2;

    private static readonly Dictionary<string, decimal> Prices = new()
    {
        ["WIDGET-1"]   = 9.99m,
        ["GIZMO-2"]    = 24.50m,
        ["GIZMO-SLOW"] = 24.50m,
        ["FLAKY-1"]    = 12.00m,
    };

    private int _flakyAttempts;

    public async Task<decimal> PriceOf(string sku)
    {
        if (sku == "GIZMO-SLOW")
        {
            Console.WriteLine($"  catalogue: looking up {sku} (this one takes {SlowLookup.TotalSeconds:0}s)");
            await Task.Delay(SlowLookup);
        }

        if (sku == "FLAKY-1")
        {
            _flakyAttempts++;
            if (_flakyAttempts <= FlakyFailures)
                throw new CatalogueUnavailableException(sku, _flakyAttempts);

            Console.WriteLine($"  catalogue: {sku} worked on attempt {_flakyAttempts}");
        }

        if (!Prices.TryGetValue(sku, out var price))
            throw new UnknownSkuException(sku);

        return price;
    }
}
