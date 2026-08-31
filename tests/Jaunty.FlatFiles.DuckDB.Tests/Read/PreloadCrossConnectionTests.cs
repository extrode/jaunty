using DuckDB.NET.Data;

using Jaunty.FlatFiles.Interfaces;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-026. Preloading is a decision one connection made at registration time, but it was
/// stored as <c>IFileSource.IsPreloaded</c> - state on an object the caller owns and may share.
/// <c>DuckDb</c>'s constructor wrote it and never cleared it, so the same source instance added to a
/// second <c>FlatFileOptions</c> with <c>PreloadIntoMemory = false</c> still registered as a TABLE,
/// still skipped the promoted-table guard, and still short-circuited promotion on the second
/// connection. Same cross-connection leak AUD-R26-069 fixed for <c>IsPromotedToTable</c>.
/// </summary>
public class PreloadCrossConnectionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _parquetPath;

    public PreloadCrossConnectionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jaunty_preload_cross_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _parquetPath = Path.Combine(_tempDir, "inventory.parquet");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using DuckDBCommand cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static string TableTypeOf(DuckDb db, string name)
    {
        using System.Data.IDbCommand cmd = db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT table_type FROM information_schema.tables WHERE table_name = '{name}'";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private IFileSource SharedSource()
    {
        var options = new FlatFileOptions();
        options.AddParquet<InventoryItem>(_parquetPath);
        return options.Sources[0];
    }

    [Fact]
    public void ASourcePreloadedOnOneConnection_IsAViewOnAConnectionThatDidNotAskForIt()
    {
        IFileSource shared = SharedSource();

        var preloading = new FlatFileOptions { PreloadIntoMemory = true };
        preloading.Sources.Add(shared);
        using var first = new DuckDb(preloading);
        Assert.Equal("BASE TABLE", TableTypeOf(first, "inventory"));

        var plain = new FlatFileOptions { PreloadIntoMemory = false };
        plain.Sources.Add(shared);
        using var second = new DuckDb(plain);

        Assert.Equal("VIEW", TableTypeOf(second, "inventory"));
    }

    [Fact]
    public void TheSecondConnectionCanStillMutate()
    {
        // The failure this leak produced: promotion short-circuited on the second connection, so
        // the DELETE ran against a view, which DuckDB rejects.
        IFileSource shared = SharedSource();

        var preloading = new FlatFileOptions { PreloadIntoMemory = true };
        preloading.Sources.Add(shared);
        using var first = new DuckDb(preloading);

        var plain = new FlatFileOptions { PreloadIntoMemory = false };
        plain.Sources.Add(shared);
        using var second = new DuckDb(plain);

        int deleted = second.Delete<InventoryItem>(i => i.ItemId == 1);

        Assert.Equal(1, deleted);
        Assert.Equal("BASE TABLE", TableTypeOf(second, "inventory"));
    }

    [Fact]
    public void TheConstructorNoLongerWritesTheCallersFlag()
    {
        IFileSource shared = SharedSource();

        var preloading = new FlatFileOptions { PreloadIntoMemory = true };
        preloading.Sources.Add(shared);
        using var db = new DuckDb(preloading);

        Assert.False(shared.IsPreloaded);
        Assert.Equal("BASE TABLE", TableTypeOf(db, "inventory"));
    }

    [Fact]
    public void ThePerSourceOptInStillWins()
    {
        IFileSource shared = SharedSource();
        shared.IsPreloaded = true;

        var plain = new FlatFileOptions { PreloadIntoMemory = false };
        plain.Sources.Add(shared);
        using var db = new DuckDb(plain);

        Assert.Equal("BASE TABLE", TableTypeOf(db, "inventory"));
    }
}
