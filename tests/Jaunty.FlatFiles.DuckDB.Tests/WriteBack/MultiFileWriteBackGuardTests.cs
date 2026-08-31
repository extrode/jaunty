using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R32: <c>Save&lt;T&gt;(WriteBackMode)</c> and its async twin reject in-place write-back for
/// sources backed by more than one file, since there is no single original file to overwrite. This
/// guard had no coverage.
/// </summary>
public class MultiFileWriteBackGuardTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_wb_multifile_{Guid.NewGuid():N}");
    private readonly string _csvPathA;
    private readonly string _csvPathB;
    private readonly DuckDb _db;

    public MultiFileWriteBackGuardTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPathA = Path.Combine(_dataDir, "inventory_a.csv");
        _csvPathB = Path.Combine(_dataDir, "inventory_b.csv");

        using var gen = new DuckDBConnection("DataSource=:memory:");
        gen.Open();
        using DuckDBCommand cmdA = gen.CreateCommand();
        cmdA.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_csvPathA.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmdA.ExecuteNonQuery();

        using DuckDBCommand cmdB = gen.CreateCommand();
        cmdB.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (2, 'Widget B', 'Electronics', 0, 49.99, false)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_csvPathB.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmdB.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>([_csvPathA, _csvPathB]);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    [Fact]
    public void Save_WithMultiFileSource_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("multi-file", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 files", ex.Message, StringComparison.Ordinal);
        Assert.Contains("output path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_WithMultiFileSource_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("multi-file", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 files", ex.Message, StringComparison.Ordinal);
        Assert.Contains("output path", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
