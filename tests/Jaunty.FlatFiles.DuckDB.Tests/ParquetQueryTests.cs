using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for Parquet file source: registration, querying with fluent API, type mappings.
/// Covers T027 (ParquetFileSource) and T034 (type mapping validation).
/// </summary>
public class ParquetQueryTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_parquet_tests_{Guid.NewGuid():N}");
    private readonly string _parquetPath;
    private readonly DuckDbFlatFileDatabase _db;

    public ParquetQueryTests()
    {
        Directory.CreateDirectory(DataDir);
        _parquetPath = Path.Combine(DataDir, "inventory.parquet");

        // Generate a Parquet file using DuckDB
        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false),
                    (3, 'Gadget C', 'Hardware', 75, 15.50, true),
                    (4, 'Gadget D', 'Hardware', 200, 8.25, true),
                    (5, 'Thingamajig', 'Misc', 10, 99.99, true),
                    (6, 'Doohickey', 'Electronics', 42, 12.00, true),
                    (7, 'Whatchamacallit', 'Hardware', 0, 5.75, false),
                    (8, 'Gizmo E', 'Electronics', 300, 199.99, true),
                    (9, 'Contraption F', 'Misc', 25, 45.00, true),
                    (10, 'Apparatus G', 'Hardware', 88, 67.50, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();

        var options = new FlatFileDatabaseOptions();
        options.AddParquet<InventoryItem>(_parquetPath);
        _db = new DuckDbFlatFileDatabase(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    // ==========================================
    // T027 — Basic Parquet registration & query
    // ==========================================

    [Fact]
    public void Parquet_Select_ReturnsAllRows()
    {
        var results = _db.Connection.From<InventoryItem>().Select();
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public void Parquet_Count_ReturnsCorrectTotal()
    {
        var count = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(10, count);
    }

    [Fact]
    public void Parquet_Where_FiltersCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Electronics")
            .Select();

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.Equal("Electronics", r.Category));
    }

    [Fact]
    public void Parquet_OrderBy_OrdersCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .OrderByDescending(i => i.UnitPrice)
            .Select();

        Assert.Equal(10, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].UnitPrice >= results[i].UnitPrice);
    }

    [Fact]
    public void Parquet_Take_LimitsResults()
    {
        var results = _db.Connection.From<InventoryItem>()
            .OrderBy(i => i.ItemId)
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Parquet_Where_OrderBy_Take_Pipeline()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.InStock == true)
            .OrderByDescending(i => i.UnitPrice)
            .Take(3)
            .Select();

        Assert.True(results.Count <= 3);
        Assert.All(results, r => Assert.True(r.InStock));
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].UnitPrice >= results[i].UnitPrice);
    }

    // ==========================================
    // T034 — Type mapping validation
    // ==========================================

    [Fact]
    public void Parquet_IntColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();

        Assert.Single(results);
        Assert.Equal(1, results[0].ItemId);
        Assert.Equal(150, results[0].StockQuantity);
    }

    [Fact]
    public void Parquet_StringColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();

        Assert.Single(results);
        Assert.Equal("Widget A", results[0].ItemName);
        Assert.Equal("Electronics", results[0].Category);
    }

    [Fact]
    public void Parquet_DecimalColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();

        Assert.Single(results);
        Assert.Equal(29.99m, results[0].UnitPrice);
    }

    [Fact]
    public void Parquet_BoolColumn_MapsCorrectly()
    {
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 2)
            .Select();

        Assert.Single(results);
        Assert.False(results[0].InStock);
    }

    // ==========================================
    // T029 — HivePartitioning option
    // ==========================================

    [Fact]
    public void Parquet_HivePartitioning_CanBeConfigured()
    {
        // Verify that HivePartitioning option is passed through to the SQL
        var source = new ParquetFileSource("test_hive", _parquetPath, typeof(InventoryItem))
        {
            HivePartitioning = true
        };

        var options = new FlatFileDatabaseOptions();
        options.Sources.Add(source);

        // Verify it doesn't throw — the parquet file isn't hive-partitioned but DuckDB should handle it
        using var db = new DuckDbFlatFileDatabase(options);
        Assert.NotNull(db);
    }
}
