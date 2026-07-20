using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// Regression tests proving the <c>CommandOptions</c> overloads of Insert/Update/Delete actually
/// apply the supplied transaction (AUD-R9), instead of silently delegating to the no-options
/// overload and running each command auto-committed outside the caller's transaction.
/// </summary>
public class CommandOptionsTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_cmdopts_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public CommandOptionsTests()
    {
        Directory.CreateDirectory(DataDir);
        _csvPath = Path.Combine(DataDir, "inventory.csv");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(_csvPath);
        _db = new DuckDb(options);

        // Force promotion from VIEW to TABLE outside of any manually-managed transaction, so the
        // transaction-scoped assertions below only observe the DML they're testing.
        _db.Delete<InventoryItem>(i => i.ItemId == -1);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    [Fact]
    public void Insert_WithTransactionOptions_RollbackUndoesInsert()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        var newItem = new InventoryItem { ItemId = 999, ItemName = "Rolled Back", Category = "Test", StockQuantity = 1, UnitPrice = 1m, InStock = true };
        _db.Insert(newItem, options);

        tx.Rollback();

        var results = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 999).Select();
        Assert.Empty(results);
    }

    [Fact]
    public void InsertMany_WithTransactionOptions_RollbackUndoesAllInserts()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        var newItems = new[]
        {
            new InventoryItem { ItemId = 998, ItemName = "A", Category = "Test", StockQuantity = 1, UnitPrice = 1m, InStock = true },
            new InventoryItem { ItemId = 997, ItemName = "B", Category = "Test", StockQuantity = 1, UnitPrice = 1m, InStock = true },
        };
        _db.Insert(newItems, options);

        tx.Rollback();

        var results = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 998 || i.ItemId == 997).Select();
        Assert.Empty(results);
    }

    [Fact]
    public void Update_WithTransactionOptions_RollbackUndoesUpdate()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        _db.Update<InventoryItem>(i => i.ItemId == 1, i => i.ItemName, "Changed", options);

        tx.Rollback();

        var result = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 1).Select().Single();
        Assert.Equal("Widget A", result.ItemName);
    }

    [Fact]
    public void Delete_WithTransactionOptions_RollbackUndoesDelete()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        _db.Delete<InventoryItem>(i => i.ItemId == 1, options);

        tx.Rollback();

        var result = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 1).Select();
        Assert.Single(result);
    }

    [Fact]
    public async Task InsertAsync_WithTransactionOptions_RollbackUndoesInsert()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        var newItem = new InventoryItem { ItemId = 996, ItemName = "Rolled Back Async", Category = "Test", StockQuantity = 1, UnitPrice = 1m, InStock = true };
        await _db.InsertAsync(newItem, options);

        tx.Rollback();

        var results = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 996).Select();
        Assert.Empty(results);
    }

    [Fact]
    public async Task UpdateAsync_WithTransactionOptions_RollbackUndoesUpdate()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        await _db.UpdateAsync<InventoryItem>(i => i.ItemId == 2, i => i.ItemName, "Changed Async", options);

        tx.Rollback();

        var result = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 2).Select().Single();
        Assert.Equal("Widget B", result.ItemName);
    }

    [Fact]
    public async Task DeleteAsync_WithTransactionOptions_RollbackUndoesDelete()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        await _db.DeleteAsync<InventoryItem>(i => i.ItemId == 2, options);

        tx.Rollback();

        var result = _db.Connection.From<InventoryItem>().Where(i => i.ItemId == 2).Select();
        Assert.Single(result);
    }
}
