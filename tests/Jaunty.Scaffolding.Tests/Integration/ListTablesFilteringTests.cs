using Jaunty.Scaffolding;
using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Configuration;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Integration;

/// <summary>
/// AUD-R35-079. <c>ListTablesAsync</c> hard-coded <c>new SchemaReaderOptions()</c>, so a caller who
/// wanted a subset - the CLI's <c>--schemas</c> being the one in the tree - could not say so, and
/// every table in the database was read in full before the unwanted ones were dropped client-side.
/// The new overload takes the filters and hands them to the reader.
/// <para>
/// SQLite ignores <c>IncludeSchemas</c> because it has no schemas, so the pass-through is pinned
/// here with <c>IncludeTables</c>/<c>ExcludeTables</c>, which its reader does apply. That the
/// filtering happens in the reader rather than after the fact is what makes it a saving on the
/// providers that do have schemas.
/// </para>
/// </summary>
public class ListTablesFilteringTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public ListTablesFilteringTests()
    {
        _connectionString = $"ListTablesFiltering_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={_connectionString};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        Execute("CREATE TABLE products (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        Execute("CREATE TABLE categories (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        Execute("CREATE TABLE orders (id INTEGER PRIMARY KEY, product_id INTEGER NOT NULL)");
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Execute(string sql)
    {
        using SqliteCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private Task<IReadOnlyList<(string Schema, string Table)>> ListAsync(SchemaReaderOptions? options)
        => new Scaffolder().ListTablesAsync(_connectionString, DatabaseProvider.SQLite, options);

    private static string[] Names(IReadOnlyList<(string Schema, string Table)> tables)
        => [.. tables.Select(t => t.Table).OrderBy(n => n, StringComparer.Ordinal)];

    [Fact]
    public async Task NoOptions_ListsEveryTable()
        => Assert.Equal(["categories", "orders", "products"], Names(await ListAsync(null)));

    [Fact]
    public async Task IncludeTables_ReachesTheReader()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await ListAsync(new SchemaReaderOptions { IncludeTables = ["products"] });

        Assert.Equal(["products"], Names(tables));
    }

    [Fact]
    public async Task ExcludeTables_ReachesTheReader()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await ListAsync(new SchemaReaderOptions { ExcludeTables = ["categories", "orders"] });

        Assert.Equal(["products"], Names(tables));
    }

    [Fact]
    public async Task AnEmptyFilterSet_IsNotTreatedAsExcludeEverything()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await ListAsync(new SchemaReaderOptions { IncludeTables = [], ExcludeTables = [] });

        Assert.Equal(["categories", "orders", "products"], Names(tables));
    }

    [Fact]
    public async Task IncludeSchemas_IsHarmlessOnSQLite()
    {
        // SQLite reports an empty schema name and its reader does not consult IncludeSchemas, so
        // the CLI keeps its client-side pass. Pinned so that adding a naive schema filter to the
        // SQLite reader cannot silently empty this listing.
        IReadOnlyList<(string Schema, string Table)> tables =
            await ListAsync(new SchemaReaderOptions { IncludeSchemas = ["dbo"] });

        Assert.Equal(["categories", "orders", "products"], Names(tables));
        Assert.All(tables, t => Assert.True(string.IsNullOrEmpty(t.Schema)));
    }

    [Fact]
    public async Task ACancelledToken_StillPropagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new Scaffolder().ListTablesAsync(
                _connectionString, DatabaseProvider.SQLite, null, cts.Token));
    }

    // ------------------------------------------------------------------
    // Control: the three-argument overload is unchanged.
    // ------------------------------------------------------------------

    [Fact]
    public async Task TheOriginalOverload_StillListsEveryTable()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await new Scaffolder().ListTablesAsync(_connectionString, DatabaseProvider.SQLite);

        Assert.Equal(["categories", "orders", "products"], Names(tables));
    }

    [Fact]
    public async Task TheOriginalOverload_StillAutoDetectsTheProvider()
    {
        IReadOnlyList<(string Schema, string Table)> tables =
            await new Scaffolder().ListTablesAsync(_connectionString);

        Assert.Equal(["categories", "orders", "products"], Names(tables));
    }
}
