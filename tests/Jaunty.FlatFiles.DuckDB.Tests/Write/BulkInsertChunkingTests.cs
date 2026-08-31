using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// Regression test for AUD-R9: <c>Insert&lt;T&gt;(IEnumerable&lt;T&gt;)</c> must chunk large
/// collections into multiple multi-row INSERT statements so the total parameter count per
/// statement stays under the DuckDB dialect's <c>MaxParametersPerStatement</c> (32768). Before the
/// fix, a single unchunked statement for a large-enough collection would exceed that limit and the
/// INSERT would fail outright.
/// </summary>
public class BulkInsertChunkingTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_chunking_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public BulkInsertChunkingTests()
    {
        Directory.CreateDirectory(DataDir);
        _csvPath = Path.Combine(DataDir, "inventory.csv");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    [Fact]
    public void Insert_CollectionExceedingMaxParametersPerStatement_ChunksAcrossMultipleStatements()
    {
        // InventoryItem has 6 mapped columns, so a single unchunked statement for this many rows
        // would need 6 * rowCount parameters — comfortably over DuckDB's 32768-parameter limit
        // for one COPY/INSERT statement (6 * 6000 = 36000). Without chunking this would throw.
        const int rowCount = 6000;
        var newItems = new InventoryItem[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            newItems[i] = new InventoryItem
            {
                ItemId = 1000 + i,
                ItemName = $"BulkItem{i}",
                Category = "Bulk",
                StockQuantity = i,
                UnitPrice = 1.23m,
                InStock = true
            };
        }

        var inserted = _db.Insert(newItems);

        Assert.Equal(rowCount, inserted);

        var results = _db.Connection.From<InventoryItem>().Where(i => i.Category == "Bulk").Select();
        Assert.Equal(rowCount, results.Count);
    }

    [Fact]
    public async Task InsertAsync_CollectionExceedingMaxParametersPerStatement_ChunksAcrossMultipleStatements()
    {
        const int rowCount = 6000;
        var newItems = new InventoryItem[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            newItems[i] = new InventoryItem
            {
                ItemId = 2000 + i,
                ItemName = $"AsyncBulkItem{i}",
                Category = "AsyncBulk",
                StockQuantity = i,
                UnitPrice = 4.56m,
                InStock = false
            };
        }

        var inserted = await _db.InsertAsync(newItems);

        Assert.Equal(rowCount, inserted);

        var results = _db.Connection.From<InventoryItem>().Where(i => i.Category == "AsyncBulk").Select();
        Assert.Equal(rowCount, results.Count);
    }
}
