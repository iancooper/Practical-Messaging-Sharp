namespace Model;

/// <summary>Permanent. This SKU does not exist and asking again will not change that.</summary>
public class UnknownSkuException(string sku)
    : Exception($"'{sku}' is not in the catalogue");

/// <summary>
/// Transient. We have no copy of the catalogue yet -- the price consumer has not started, or
/// has not caught up. The SKU may be perfectly good; we are simply not ready to price it.
///
/// **This is a different fact from <see cref="UnknownSkuException"/> and the difference is the
/// point of Probe C.** One of them is about the order and one of them is about us.
/// </summary>
public class LocalCopyEmptyException(string sku)
    : Exception($"cannot price '{sku}': the local copy has no prices in it yet");

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
/// </summary>
public class Catalogue(IPriceStore prices)
{
    public async Task<decimal> PriceOf(string sku)
    {
        var price = await prices.Lookup(sku);

        if (price is not null)
            return price.Amount;

        // Two different failures wear the same shape -- a lookup that returned nothing -- and
        // exercise 2 spent forty minutes on why that matters. "I have no copy yet" is about us
        // and will fix itself; "that SKU is not a thing" is about the order and never will.
        //
        // **The domain's job is to say which.** What to do about each is the pump's policy and
        // not ours, and Probe C is about the fact that the pump currently does the same thing
        // with both.
        if (await prices.Count() == 0)
            throw new LocalCopyEmptyException(sku);

        throw new UnknownSkuException(sku);
    }
}
