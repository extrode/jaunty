using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for PreloadIntoMemory option (CREATE TABLE AS vs VIEW).
/// Covers T031 (PreloadIntoMemory) and T038 (VIEW vs TABLE comparison).
/// </summary>
public class PreloadTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly string _tempDir;
    private readonly string _parquetPath;

    public PreloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jaunty_preload_test_{Guid.NewGuid():N}");
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
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ==========================================
    // T031 — PreloadIntoMemory per-source
    // ==========================================

    [Fact]
    public void Preload_PerSource_CreatesTable()
    {
        var options = new FlatFileDatabaseOptions();
        options.AddParquet<InventoryItem>(_parquetPath, parquet =>
        {
            parquet.IsPreloaded = true;
        });

        using var db = new DuckDbFlatFileDatabase(options);

        // Verify it was created as a TABLE (not VIEW)
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'inventory'";
        var tableType = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("BASE TABLE", tableType);
    }

    [Fact]
    public void Preload_PerSource_QueriesWork()
    {
        var options = new FlatFileDatabaseOptions();
        options.AddParquet<InventoryItem>(_parquetPath, parquet =>
        {
            parquet.IsPreloaded = true;
        });

        using var db = new DuckDbFlatFileDatabase(options);
        var results = db.Connection.From<InventoryItem>().Select();
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void NoPreload_CreatesView()
    {
        var options = new FlatFileDatabaseOptions();
        options.AddParquet<InventoryItem>(_parquetPath);

        using var db = new DuckDbFlatFileDatabase(options);

        // Verify it was created as a VIEW
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'inventory'";
        var tableType = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("VIEW", tableType);
    }

    // ==========================================
    // T031 — PreloadIntoMemory global option
    // ==========================================

    [Fact]
    public void Preload_GlobalOption_AppliesToAllSources()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");

        var options = new FlatFileDatabaseOptions
        {
            PreloadIntoMemory = true
        };
        options.AddCsv<SalesRecord>(csvPath);
        options.AddParquet<InventoryItem>(_parquetPath);
        options.AddJson<CustomerProfile>(jsonPath);

        using var db = new DuckDbFlatFileDatabase(options);

        // All should be tables, not views
        using var cmd = db.Connection.CreateCommand();

        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'sales'";
        Assert.Equal("BASE TABLE", cmd.ExecuteScalar()?.ToString());

        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'inventory'";
        Assert.Equal("BASE TABLE", cmd.ExecuteScalar()?.ToString());

        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'customers'";
        Assert.Equal("BASE TABLE", cmd.ExecuteScalar()?.ToString());
    }

    [Fact]
    public void Preload_GlobalOption_AllSourcesQueryable()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");

        var options = new FlatFileDatabaseOptions
        {
            PreloadIntoMemory = true
        };
        options.AddCsv<SalesRecord>(csvPath);
        options.AddParquet<InventoryItem>(_parquetPath);
        options.AddJson<CustomerProfile>(jsonPath);

        using var db = new DuckDbFlatFileDatabase(options);

        var salesCount = db.Connection.From<SalesRecord>().Count();
        var itemCount = db.Connection.From<InventoryItem>().Count();
        var customerCount = db.Connection.From<CustomerProfile>().Count();

        Assert.True(salesCount > 0);
        Assert.Equal(3, itemCount);
        Assert.Equal(3, customerCount);
    }

    // ==========================================
    // T031 — Preload with CSV
    // ==========================================

    [Fact]
    public void Preload_Csv_CreatesTableAndQueries()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileDatabaseOptions();
        options.AddCsv<SalesRecord>(csvPath, csv =>
        {
            csv.IsPreloaded = true;
        });

        using var db = new DuckDbFlatFileDatabase(options);

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'sales'";
        Assert.Equal("BASE TABLE", cmd.ExecuteScalar()?.ToString());

        var results = db.Connection.From<SalesRecord>().Select();
        Assert.NotEmpty(results);
    }

    // ==========================================
    // T031 — Preload with JSON
    // ==========================================

    [Fact]
    public void Preload_Json_CreatesTableAndQueries()
    {
        var jsonPath = Path.Combine(DataDir, "json", "customers.json");
        var options = new FlatFileDatabaseOptions();
        options.AddJson<CustomerProfile>(jsonPath, json =>
        {
            json.IsPreloaded = true;
        });

        using var db = new DuckDbFlatFileDatabase(options);

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'customers'";
        Assert.Equal("BASE TABLE", cmd.ExecuteScalar()?.ToString());

        var results = db.Connection.From<CustomerProfile>().Select();
        Assert.Equal(3, results.Count);
    }

    // ==========================================
    // T038 — VIEW vs TABLE both return correct data
    // ==========================================

    [Fact]
    public void ViewVsTable_BothReturnSameData()
    {
        // Create VIEW-based db
        var viewOptions = new FlatFileDatabaseOptions();
        viewOptions.AddParquet<InventoryItem>(_parquetPath);
        using var viewDb = new DuckDbFlatFileDatabase(viewOptions);
        var viewResults = viewDb.Connection.From<InventoryItem>()
            .OrderBy(i => i.ItemId)
            .Select();

        // Create TABLE-based db
        var tableOptions = new FlatFileDatabaseOptions();
        tableOptions.AddParquet<InventoryItem>(_parquetPath, p => p.IsPreloaded = true);
        using var tableDb = new DuckDbFlatFileDatabase(tableOptions);
        var tableResults = tableDb.Connection.From<InventoryItem>()
            .OrderBy(i => i.ItemId)
            .Select();

        // Same data
        Assert.Equal(viewResults.Count, tableResults.Count);
        for (int i = 0; i < viewResults.Count; i++)
        {
            Assert.Equal(viewResults[i].ItemId, tableResults[i].ItemId);
            Assert.Equal(viewResults[i].ItemName, tableResults[i].ItemName);
            Assert.Equal(viewResults[i].UnitPrice, tableResults[i].UnitPrice);
        }
    }
}
