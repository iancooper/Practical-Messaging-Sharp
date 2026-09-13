using System.Globalization;
using Microsoft.Data.Sqlite;
using Model;

namespace LocalCopy;

/// <summary>
/// The local copy of the catalogue's prices, in a SQLite file.
///
/// **A file is the cheapest durable store there is**, and durable is the only property the
/// exercise actually needs: the price consumer that fills this runs in its own process, and
/// Probes B, C and D all turn on what is still here after that process dies.
///
/// It implements <see cref="IPriceStore"/>, which Model declares, so the domain reads prices
/// without knowing any of this exists. It also exposes <see cref="Apply"/>, which Model does
/// *not* know about: reading the copy is the domain's business, and maintaining it is the
/// price consumer's.
/// </summary>
public sealed class SqlitePriceStore : IPriceStore, IDisposable
{
    /// <summary>Sits next to whatever you ran, so all four processes share one copy.</summary>
    public const string DefaultPath = "prices.db";

    private readonly SqliteConnection _connection;

    private SqlitePriceStore(SqliteConnection connection) => _connection = connection;

    public static async Task<SqlitePriceStore> OpenAsync(string path = DefaultPath)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();

        // WAL, because a reader and a writer are two different processes here and the default
        // journal would have them locking each other out. This is a real decision and not
        // boilerplate: "my local copy is a file" stops being free the moment two processes
        // want it at once.
        await Execute(connection, "PRAGMA journal_mode=WAL");
        await Execute(connection, "PRAGMA busy_timeout=5000");

        // The price is TEXT rather than REAL on purpose. A price is a decimal and SQLite's
        // REAL is a double, and 9.99 is not a double. Storing money in a float is a bug that
        // takes months to surface and this is the line that prevents it.
        await Execute(connection, """
            CREATE TABLE IF NOT EXISTS prices (
                sku        TEXT PRIMARY KEY,
                price      TEXT NOT NULL,
                changed_at TEXT NOT NULL,
                applied_at TEXT NOT NULL
            )
            """);

        return new SqlitePriceStore(connection);
    }

    public async Task<Price?> Lookup(string sku)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT price, changed_at, applied_at FROM prices WHERE sku = $sku";
        command.Parameters.AddWithValue("$sku", sku);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Price(
            sku,
            decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture));
    }

    public async Task<int> Count()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM prices";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// Write a price change into the copy. **Last writer wins**, which is only safe because
    /// <see cref="PriceChanged"/> is a snapshot -- run this twice with the same event and the
    /// row ends up the same. That is Probe D's whole payout, and it was decided in step 1.
    /// </summary>
    /// <returns>When the row was written, for Probe A's arithmetic.</returns>
    public async Task<DateTimeOffset> Apply(PriceChanged @event)
    {
        var appliedAt = DateTimeOffset.UtcNow;

        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO prices (sku, price, changed_at, applied_at)
            VALUES ($sku, $price, $changed_at, $applied_at)
            ON CONFLICT(sku) DO UPDATE SET
                price      = excluded.price,
                changed_at = excluded.changed_at,
                applied_at = excluded.applied_at
            """;
        command.Parameters.AddWithValue("$sku", @event.Sku);
        command.Parameters.AddWithValue("$price", @event.Price.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$changed_at", @event.ChangedAt.ToString("O"));
        command.Parameters.AddWithValue("$applied_at", appliedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();

        return appliedAt;
    }

    public async Task<DateTimeOffset?> NewestChangedAt()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT MAX(changed_at) FROM prices";
        var value = await command.ExecuteScalarAsync();
        return value is string text
            ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture)
            : null;
    }

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose() => _connection.Dispose();
}
