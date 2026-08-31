using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-244. <c>ExistsAsTable</c> matched on <c>table_name</c> alone across all of
/// <c>information_schema.tables</c>, while every object <c>DuckDb</c> creates is unqualified. A base
/// table of that name in another catalog therefore sent <c>RegisterSource</c> down the "already
/// promoted, leave it alone" branch, the view was never created, and queries for that entity read
/// whatever the unqualified name resolved to instead of the registered file.
/// </summary>
public class ExistsAsTableScopeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _parquetPath;

    public ExistsAsTableScopeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jaunty_exists_scope_{Guid.NewGuid():N}");
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

    private static void Execute(DuckDb db, string sql)
    {
        using System.Data.IDbCommand cmd = db.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string TableTypeOf(DuckDb db, string name)
    {
        using System.Data.IDbCommand cmd = db.Connection.CreateCommand();
        cmd.CommandText =
            "SELECT table_type FROM information_schema.tables " +
            $"WHERE table_name = '{name}' AND table_schema = current_schema() AND table_catalog = current_database()";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static DuckDb OpenEmpty() => new(new FlatFileOptions());

    private void RegisterInventory(DuckDb db)
    {
        var options = new FlatFileOptions();
        options.AddParquet<InventoryItem>(_parquetPath);
        db.RegisterSource(options.Sources[0]);
    }

    [Fact]
    public void ASameNamedTableInAnAttachedCatalog_DoesNotSuppressTheView()
    {
        using DuckDb db = OpenEmpty();
        Execute(db, "ATTACH ':memory:' AS other");
        Execute(db, "CREATE TABLE other.main.inventory AS SELECT 99 AS \"ItemId\"");

        RegisterInventory(db);

        Assert.Equal("VIEW", TableTypeOf(db, "inventory"));
        Assert.Equal(2, db.Query<InventoryItem>().Count());
    }

    [Fact]
    public async Task ASameNamedTableInAnAttachedCatalog_DoesNotSuppressTheViewAsync()
    {
        await using DuckDb db = OpenEmpty();
        Execute(db, "ATTACH ':memory:' AS other");
        Execute(db, "CREATE TABLE other.main.inventory AS SELECT 99 AS \"ItemId\"");

        var options = new FlatFileOptions();
        options.AddParquet<InventoryItem>(_parquetPath);
        await db.RegisterSourceAsync(options.Sources[0]);

        Assert.Equal("VIEW", TableTypeOf(db, "inventory"));
        Assert.Equal(2, db.Query<InventoryItem>().Count());
    }

    [Fact]
    public void ASameNamedTableInTheCurrentSchema_StillSuppressesTheView()
    {
        // The guard's real job, unchanged: a promotion that persisted in the catalog this
        // connection writes to must be left alone rather than replaced by a view.
        using DuckDb db = OpenEmpty();
        Execute(db, "CREATE TABLE inventory AS SELECT 99 AS \"ItemId\"");

        RegisterInventory(db);

        Assert.Equal("BASE TABLE", TableTypeOf(db, "inventory"));
    }

    [Fact]
    public void ATemporaryTable_IsNotABaseTableAndSoNeverMatched()
    {
        // Measured against the pinned DuckDB: a TEMPORARY table is reported as table_catalog
        // 'temp', table_schema 'main', table_type 'LOCAL TEMPORARY'. The guard's pre-existing
        // table_type = 'BASE TABLE' filter therefore already excluded it, so the temp half of the
        // finding was never reachable and only the attached-catalog half above was. Pinned so the
        // next reader does not re-derive it, and so relaxing that filter is caught.
        using DuckDb db = OpenEmpty();
        Execute(db, "CREATE TEMPORARY TABLE inventory AS SELECT 99 AS \"ItemId\"");

        using System.Data.IDbCommand cmd = db.Connection.CreateCommand();
        cmd.CommandText =
            "SELECT table_catalog || '|' || table_schema || '|' || table_type " +
            "FROM information_schema.tables WHERE table_name = 'inventory'";

        Assert.Equal("temp|main|LOCAL TEMPORARY", cmd.ExecuteScalar()?.ToString());

        RegisterInventory(db);

        Assert.Equal("VIEW", TableTypeOf(db, "inventory"));
    }
}
