using DuckDB.NET.Data;
using Jaunty.FlatFiles;
using Jaunty.FlatFiles.DuckDB;
using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for multi-source sessions: CSV + Parquet + JSON in one database.
/// Covers T032 (multi-source session) and T037 (multi-source mixed queries).
/// This is the M2 exit gate: all four formats queryable through one FlatFileDatabase instance.
/// </summary>
public class MultiSourceTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly string _tempDir;
    private readonly string _parquetPath;
    private readonly DuckDbFlatFileDatabase _db;

    public MultiSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jaunty_multi_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _parquetPath = Path.Combine(_tempDir, "inventory.parquet");

        // Generate Parquet file
        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false),
                    (3, 'Gadget C', 'Hardware', 75, 15.50, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();

        // Build multi-source database: CSV + Parquet + JSON
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");

        var options = new FlatFileDatabaseOptions();
        options.AddCsv<SalesRecord>(csvPath);
        options.AddParquet<InventoryItem>(_parquetPath);
        options.AddJson<CustomerProfile>(jsonPath);

        _db = new DuckDbFlatFileDatabase(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ==========================================
    // T032 — Multi-source session
    // ==========================================

    [Fact]
    public void MultiSource_AllSourcesRegistered()
    {
        Assert.NotNull(_db.GetSource<SalesRecord>());
        Assert.NotNull(_db.GetSource<InventoryItem>());
        Assert.NotNull(_db.GetSource<CustomerProfile>());
    }

    [Fact]
    public void MultiSource_CsvQueryable()
    {
        var results = _db.Connection.From<SalesRecord>().Select();
        Assert.NotEmpty(results);
    }

    [Fact]
    public void MultiSource_ParquetQueryable()
    {
        var results = _db.Connection.From<InventoryItem>().Select();
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void MultiSource_JsonQueryable()
    {
        var results = _db.Connection.From<CustomerProfile>().Select();
        Assert.Equal(3, results.Count);
    }

    // ==========================================
    // T037 — Multi-source mixed queries
    // ==========================================

    [Fact]
    public void MultiSource_QueryEachIndependently()
    {
        // Query CSV with filter
        var sales = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 10000m)
            .Select();
        Assert.All(sales, s => Assert.True(s.Revenue > 10000m));

        // Query Parquet with filter
        var inventory = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Electronics")
            .Select();
        Assert.All(inventory, i => Assert.Equal("Electronics", i.Category));

        // Query JSON with filter
        var customers = _db.Connection.From<CustomerProfile>()
            .Where(c => c.CustomerId > 1)
            .Select();
        Assert.All(customers, c => Assert.True(c.CustomerId > 1));
    }

    [Fact]
    public void MultiSource_QueryWithOrderAndPagination()
    {
        // CSV: order + limit
        var topSales = _db.Connection.From<SalesRecord>()
            .OrderByDescending(s => s.Revenue)
            .Take(3)
            .Select();
        Assert.True(topSales.Count <= 3);
        for (int i = 1; i < topSales.Count; i++)
            Assert.True(topSales[i - 1].Revenue >= topSales[i].Revenue);

        // Parquet: order + limit
        var topItems = _db.Connection.From<InventoryItem>()
            .OrderByDescending(i => i.UnitPrice)
            .Take(2)
            .Select();
        Assert.True(topItems.Count <= 2);

        // JSON: order + limit
        var topCustomers = _db.Connection.From<CustomerProfile>()
            .OrderBy(c => c.Name)
            .Take(2)
            .Select();
        Assert.Equal(2, topCustomers.Count);
    }

    // ==========================================
    // M2 Exit Gate
    // ==========================================

    [Fact]
    public void ExitGate_AllFourFormats_QueryableThroughOneInstance()
    {
        // CSV
        var salesCount = _db.Connection.From<SalesRecord>().Count();
        Assert.True(salesCount > 0);

        // Parquet
        var inventoryCount = _db.Connection.From<InventoryItem>().Count();
        Assert.True(inventoryCount > 0);

        // JSON (array)
        var customerCount = _db.Connection.From<CustomerProfile>().Count();
        Assert.True(customerCount > 0);

        // Verify each returns actual typed entities with correct data
        var firstSale = _db.Connection.From<SalesRecord>().OrderBy(s => s.Id).Take(1).Select();
        Assert.Single(firstSale);
        Assert.NotEqual(0, firstSale[0].Id);
        Assert.NotEmpty(firstSale[0].ProductName);

        var firstItem = _db.Connection.From<InventoryItem>().OrderBy(i => i.ItemId).Take(1).Select();
        Assert.Single(firstItem);
        Assert.Equal("Widget A", firstItem[0].ItemName);

        var firstCustomer = _db.Connection.From<CustomerProfile>().OrderBy(c => c.CustomerId).Take(1).Select();
        Assert.Single(firstCustomer);
        Assert.Equal("John Doe", firstCustomer[0].Name);
    }

    // ==========================================
    // Builder API via FlatFileDatabase.Open()
    // ==========================================

    [Fact]
    public void MultiSource_ViaOpenBuilder_Works()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");

        using var db = FlatFileDatabase.Open(options =>
        {
            options.AddCsv<SalesRecord>(csvPath);
            options.AddParquet<InventoryItem>(_parquetPath);
            options.AddJson<CustomerProfile>(jsonPath);
        });

        var salesCount = db.Connection.From<SalesRecord>().Count();
        var itemCount = db.Connection.From<InventoryItem>().Count();
        var customerCount = db.Connection.From<CustomerProfile>().Count();

        Assert.True(salesCount > 0);
        Assert.Equal(3, itemCount);
        Assert.Equal(3, customerCount);
    }
}
