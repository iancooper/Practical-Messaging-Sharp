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
/// Note that it answers three different questions, not one. "Have you a price for this?" is the
/// obvious one. "Have you any prices at all?" separates *the SKU is unknown* from *we are not
/// ready yet*, which is Probe C. "How old is the newest thing you have?" is the only question
/// that can tell a current copy from a stale one, and it is the one nothing asks often enough.
///
/// **A lookup that can only say yes or no cannot be operated.** That is a design decision you
/// make when you write the interface, long before anybody needs the answer.
/// </summary>
public interface IPriceStore
{
    /// <summary>The price for this SKU, or null if the local copy does not have one.</summary>
    Task<Price?> Lookup(string sku);

    /// <summary>
    /// How many prices the copy holds. **Zero means "I have never been filled"**, which is not
    /// the same fact as "that SKU is not a thing" and must not produce the same behaviour.
    /// </summary>
    Task<int> Count();

    /// <summary>
    /// When the catalogue last changed something we know about, or null if the copy is empty.
    ///
    /// **This is the number Probe C is really about.** A copy that cannot say how old it is
    /// cannot be monitored, and a copy that cannot be monitored is one you find out about from
    /// a customer.
    /// </summary>
    Task<DateTimeOffset?> NewestChangedAt();
}
