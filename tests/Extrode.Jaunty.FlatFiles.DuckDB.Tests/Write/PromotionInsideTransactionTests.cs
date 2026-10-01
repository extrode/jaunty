using DuckDB.NET.Data;

using Extrode.Jaunty.Core;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Extrode.Jaunty.FlatFiles.FileSources;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// AUD-R38-002: the first mutation of an unpromoted source under a caller's transaction threw
/// "Already in a transaction." because promotion always began its own.
/// AUD-R38-018: <see cref="DuckDb.RegisterSource"/> skipped the table-name uniqueness guard.
/// </summary>
public class PromotionInsideTransactionTests : IDisposable
{
    private const string SelectAll = "SELECT * FROM inventory ORDER BY \"ItemId\"";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_promote_tx_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public PromotionInsideTransactionTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPath = Path.Combine(_dataDir, "inventory.csv");

        using (var gen = new DuckDBConnection("DataSource=:memory:"))
        {
            gen.Open();
            using DuckDBCommand cmd = gen.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget A', 'Electronics', 150, 29.99, true),
                        (2, 'Widget B', 'Electronics', 0, 49.99, false)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    private string TableType()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT table_type FROM information_schema.tables WHERE table_name = 'inventory'";
        return (string)cmd.ExecuteScalar()!;
    }

    [Fact]
    public void FirstDelete_WithAPassedTransaction_RunsAndCommits()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            Assert.Equal(1, _db.Delete<InventoryItem>(i => i.ItemId == 2, CommandOptions.WithTransaction(tx)));
            tx.Commit();
        }

        Assert.Equal("BASE TABLE", TableType());
        Assert.Equal(1, _db.Delete<InventoryItem>(i => i.ItemId == 1));
        Assert.Empty(_db.Query<InventoryItem>(SelectAll));
    }

    [Fact]
    public void FirstDelete_WithAPassedTransaction_RolledBack_LeavesTheViewAndLaterMutationsWork()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            Assert.Equal(1, _db.Delete<InventoryItem>(i => i.ItemId == 2, CommandOptions.WithTransaction(tx)));
            Assert.Equal(1, _db.Delete<InventoryItem>(i => i.ItemId == 1, CommandOptions.WithTransaction(tx)));
            tx.Rollback();
        }

        Assert.Equal("VIEW", TableType());
        Assert.Equal(2, _db.Query<InventoryItem>(SelectAll).Count);
        Assert.Equal(1, _db.Delete<InventoryItem>(i => i.ItemId == 2));
        Assert.Equal("BASE TABLE", TableType());
    }

    [Fact]
    public void FirstUpdate_InsideAnUnpassedTransaction_JoinsIt()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            Assert.Equal(1, _db.Update<InventoryItem>(i => i.ItemId == 1, i => i.Category, "Moved"));
            tx.Rollback();
        }

        Assert.Equal("VIEW", TableType());
        Assert.Equal("Electronics", _db.Query<InventoryItem>(SelectAll)[0].Category);
    }

    [Fact]
    public void FirstInsert_WithAPassedTransaction_RunsAndCommits()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            var item = new InventoryItem { ItemId = 3, ItemName = "Widget C", Category = "Test", StockQuantity = 1, UnitPrice = 1m, InStock = true };
            Assert.Equal(1, _db.Insert(item, CommandOptions.WithTransaction(tx)));
            tx.Commit();
        }

        Assert.Equal(3, _db.Query<InventoryItem>(SelectAll).Count);
    }

    [Fact]
    public async Task FirstDeleteAsync_WithAPassedTransaction_RunsAndCommits()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            Assert.Equal(1, await _db.DeleteAsync<InventoryItem>(i => i.ItemId == 2, CommandOptions.WithTransaction(tx)));
            tx.Commit();
        }

        Assert.Equal("BASE TABLE", TableType());
        Assert.Single(_db.Query<InventoryItem>(SelectAll));
    }

    [Fact]
    public async Task FirstUpdateAsync_RolledBack_LeavesTheView()
    {
        using (var tx = _db.Connection.BeginTransaction())
        {
            Assert.Equal(1, await _db.UpdateAsync<InventoryItem>(i => i.ItemId == 1, i => i.Category, "Moved", CommandOptions.WithTransaction(tx)));
            tx.Rollback();
        }

        Assert.Equal("VIEW", TableType());
        Assert.Equal(1, await _db.UpdateAsync<InventoryItem>(i => i.ItemId == 1, i => i.Category, "Moved"));
        Assert.Equal("BASE TABLE", TableType());
    }

    [Fact]
    public void RegisterSource_WithAnotherEntitysTableName_Throws()
    {
        var clash = new CsvFileSource("Inventory", _csvPath, typeof(SalesRecord));

        var ex = Assert.Throws<InvalidOperationException>(() => _db.RegisterSource(clash));

        Assert.StartsWith("A file source for table 'Inventory' is already registered (entity 'InventoryItem'", ex.Message);
        Assert.Equal(2, _db.Query<InventoryItem>(SelectAll).Count);
    }

    [Fact]
    public async Task RegisterSourceAsync_WithAnotherEntitysTableName_Throws()
    {
        var clash = new CsvFileSource("inventory", _csvPath, typeof(SalesRecord));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.RegisterSourceAsync(clash).AsTask());
    }

    [Fact]
    public void RegisterSource_ForTheSameEntity_StillReplaces()
    {
        _db.RegisterSource(new CsvFileSource("inventory", _csvPath, typeof(InventoryItem)));

        Assert.Equal(2, _db.Query<InventoryItem>(SelectAll).Count);
    }
}
