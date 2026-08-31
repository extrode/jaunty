using System.Linq.Expressions;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// AUD-R35-262 and AUD-R35-263. None of the ten <c>ArgumentNullException.ThrowIfNull</c> guards on
/// the write paths - predicate x4, column x2, entity x2, entities x2 - had a test; the whole
/// <c>Write/</c> test directory contained no <c>ArgumentNullException</c> assertion. And
/// <c>InsertAsync&lt;T&gt;(IEnumerable&lt;T&gt;, CommandOptions, CancellationToken)</c> was the one
/// <c>CommandOptions</c> write overload with no test that passed options, while the per-chunk
/// <c>ThrowIfCancellationRequested</c> in the same method had no cancelled-token test on any write.
/// </summary>
public class WriteArgumentGuardTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_write_guards_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public WriteArgumentGuardTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "inventory.csv");

        using (var genConnection = new DuckDBConnection("DataSource=:memory:"))
        {
            genConnection.Open();
            using DuckDBCommand cmd = genConnection.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget A', 'Electronics', 150, 29.99, true),
                        (2, 'Widget B', 'Electronics', 0, 49.99, false)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(csvPath);
        _db = new DuckDb(options);

        // Promote outside any manually-managed transaction, as the sibling write tests do.
        _db.Delete<InventoryItem>(i => i.ItemId == -1);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static InventoryItem Item(int id) => new()
    {
        ItemId = id,
        ItemName = $"Item {id}",
        Category = "Electronics",
        StockQuantity = 1,
        UnitPrice = 1.5m,
        InStock = true
    };

    [Fact]
    public void Delete_NullPredicate_Throws()
    {
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(
            () => _db.Delete<InventoryItem>(null!)).ParamName);

        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(
            () => _db.Delete<InventoryItem>(null!, default(CommandOptions))).ParamName);
    }

    [Fact]
    public async Task DeleteAsync_NullPredicate_Throws()
    {
        Assert.Equal("predicate", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.DeleteAsync<InventoryItem>(null!).AsTask())).ParamName);

        Assert.Equal("predicate", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.DeleteAsync<InventoryItem>(null!, default(CommandOptions)).AsTask())).ParamName);
    }

    [Fact]
    public void Update_NullPredicateOrColumn_Throws()
    {
        Expression<Func<InventoryItem, object>> column = i => i.StockQuantity;

        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(
            () => _db.Update<InventoryItem>(null!, column, 5)).ParamName);

        Assert.Equal("column", Assert.Throws<ArgumentNullException>(
            () => _db.Update<InventoryItem>(i => i.ItemId == 1, null!, 5)).ParamName);
    }

    [Fact]
    public async Task UpdateAsync_NullPredicateOrColumn_Throws()
    {
        Expression<Func<InventoryItem, object>> column = i => i.StockQuantity;

        Assert.Equal("predicate", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.UpdateAsync<InventoryItem>(null!, column, 5).AsTask())).ParamName);

        Assert.Equal("column", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.UpdateAsync<InventoryItem>(i => i.ItemId == 1, null!, 5).AsTask())).ParamName);
    }

    [Fact]
    public void Insert_NullEntityOrCollection_Throws()
    {
        Assert.Equal("entity", Assert.Throws<ArgumentNullException>(
            () => _db.Insert<InventoryItem>((InventoryItem)null!)).ParamName);

        Assert.Equal("entities", Assert.Throws<ArgumentNullException>(
            () => _db.Insert<InventoryItem>((IEnumerable<InventoryItem>)null!)).ParamName);
    }

    [Fact]
    public async Task InsertAsync_NullEntityOrCollection_Throws()
    {
        Assert.Equal("entity", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.InsertAsync<InventoryItem>((InventoryItem)null!).AsTask())).ParamName);

        Assert.Equal("entities", (await Assert.ThrowsAsync<ArgumentNullException>(
            () => _db.InsertAsync<InventoryItem>((IEnumerable<InventoryItem>)null!).AsTask())).ParamName);
    }

    [Fact]
    public async Task InsertManyAsync_WithTransactionOptions_RollbackUndoesAllInserts()
    {
        int before = _db.Query<InventoryItem>("SELECT * FROM \"inventory\"").Count;

        using (var transaction = (DuckDBTransaction)_db.Connection.BeginTransaction())
        {
            await _db.InsertAsync(
                [Item(90), Item(91), Item(92)],
                CommandOptions.WithTransaction(transaction));

            transaction.Rollback();
        }

        Assert.Equal(before, _db.Query<InventoryItem>("SELECT * FROM \"inventory\"").Count);
    }

    [Fact]
    public async Task InsertManyAsync_WithNonDbTransaction_ThrowsArgumentException()
    {
        using var real = (DuckDBTransaction)_db.Connection.BeginTransaction();
        using var wrapper = new Helpers.IDbTransactionWrapper(real);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _db.InsertAsync([Item(93)], CommandOptions.WithTransaction(wrapper)).AsTask());

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);

        real.Rollback();
    }

    [Fact]
    public async Task InsertManyAsync_WithACancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _db.InsertAsync([Item(94), Item(95)], cts.Token).AsTask());
    }

    [Fact]
    public async Task InsertManyAsync_WithACancelledTokenAndOptions_ThrowsToo()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _db.InsertAsync([Item(96)], default(CommandOptions), cts.Token).AsTask());
    }

    [Fact]
    public async Task InsertManyAsync_WithALiveToken_StillInserts()
    {
        int before = _db.Query<InventoryItem>("SELECT * FROM \"inventory\"").Count;

        int inserted = await _db.InsertAsync([Item(97), Item(98)], default(CommandOptions), CancellationToken.None);

        Assert.Equal(2, inserted);
        Assert.Equal(before + 2, _db.Query<InventoryItem>("SELECT * FROM \"inventory\"").Count);
    }
}
