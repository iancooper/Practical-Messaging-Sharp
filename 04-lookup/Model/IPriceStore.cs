namespace Model;

/// <summary>One row of the local copy: a price, and when it became true and when we heard.</summary>
/// <param name="Sku">What it is a price for.</param>
/// <param name="Amount">The price itself.</param>
/// <param name="ChangedAt">When the catalogue service says it changed. Comes off the event.</param>
/// <param name="AppliedAt">When *we* wrote it down. The gap between the two is Probe A.</param>
public record Price(string Sku, decimal Amount, DateTimeOffset ChangedAt, DateTimeOffset AppliedAt);

/// <summary>
/// The local copy of somebody else's reference data, as the domain sees it.
///
/// ---------------------------------------------------------------------------------------
///  **This interface is declared by Model, and that is the whole point of it.**
///
///  Model.csproj references SimpleMessaging and nothing else. It does not know that the copy
///  is SQLite, that it is a file, or that a separate process fills it -- LocalCopy knows all
///  three, Receiver puts the two together, and the domain names none of it.
///
///  It is exercise 1's fix arriving a second time, against a storage technology instead of a
///  broker, and the seam did not have to change to cope. Put Microsoft.Data.Sqlite in
///  Model.csproj and you have undone it.
/// ---------------------------------------------------------------------------------------
///
/// Note that it can answer two different questions -- "have you a price for this?" and "have
/// you any prices at all?" -- because those are different facts and Probe C is about telling
/// them apart. Whether the caller *asks* both is another matter: see Catalogue.
/// </summary>
public interface IPriceStore
{
    /// <summary>The price for this SKU, or null if the local copy does not have one.</summary>
    Task<Price?> Lookup(string sku);

    /// <summary>
    /// How many prices the copy holds. **Zero means "I have never been filled"**, which is not
    /// the same fact as "that SKU is not a thing" and should not produce the same behaviour.
    /// </summary>
    Task<int> Count();
}
