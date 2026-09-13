namespace Model;

/// <summary>Permanent. This SKU does not exist and asking again will not change that.</summary>
public class UnknownSkuException(string sku)
    : Exception($"'{sku}' is not in the catalogue");

/// <summary>
/// Reference data the handler needs: what does this SKU cost?
///
/// **Everything about how this answers has changed, and its signature has not.** In exercises
/// 1 to 3 it was a dictionary that pretended to be a service call, and it failed in three ways:
/// slow (GIZMO-SLOW), briefly unwell (FLAKY-1) and unknown. Two of those three are gone, and
/// their going is the lesson -- they were *on-demand* failures, and there is no longer a call
/// to be slow or unwell. Get It In Advance did not fix them. It removed the thing that could
/// fail, and bought you Probe B instead.
///
/// The handler did not change. It still asks the catalogue for a price, and the catalogue
/// still decides where prices come from. That is what the seam was for.
///
/// ---------------------------------------------------------------------------------------
///  **PROBE C IS ABOUT THE FOUR LINES OF PriceOf, AND THEY ARE THE EASY ANSWER.**
///
///  The store can tell you whether it has any prices at all. This does not ask. So an empty
///  local copy -- the price consumer has never run, or has not caught up -- comes out of here
///  as UnknownSkuException, which is a *permanent* failure, which the pump will retry n times
///  and then dead-letter. The order is thrown away because the lookup was not ready yet.
///
///  Exercise 2 taught you that a failure to understand and a failure to process need different
///  destinations. This is the same distinction one layer down, and the fix is small. Run Probe
///  C before you read SOLUTION.md.
/// ---------------------------------------------------------------------------------------
/// </summary>
public class Catalogue(IPriceStore prices)
{
    public async Task<decimal> PriceOf(string sku)
    {
        var price = await prices.Lookup(sku);

        if (price is null)
            throw new UnknownSkuException(sku);

        return price.Amount;
    }
}
