using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// AUD-R35-077. Every write-path <see cref="CommandOptions"/> test in
/// <c>Write/CommandOptionsTests.cs</c> opens a transaction on <c>_db.Connection</c>, performs the
/// write, rolls back and asserts the write is gone - the round-32 vacuous pattern recorded in
/// <c>00-carry-forward.md</c>. DuckDB binds a command to its connection's open transaction
/// implicitly, so all of those pass whether or not <c>options</c> reaches
/// <c>NonQueryExecutor.ApplyOptions</c>. The read path was given a plumbing-only proof for exactly
/// this when AUD-R32-009 landed (<c>Read/QueryCommandOptionsTests</c>); the six write entry points
/// never got the equivalent, so the <c>options</c>-forwarding fixed by AUD-R9-009 item 12 was
/// unpinned at the <c>DuckDb</c> level.
/// <para>
/// The proof is the same one the read path uses: an <see cref="IDbTransactionWrapper"/> is a valid
/// <c>IDbTransaction</c> that is not a <c>DbTransaction</c>, so it can only produce an
/// <see cref="ArgumentException"/> by having reached <c>ApplyOptions</c>. A test that merely
/// rolled back would be green with the argument dropped on the floor.
/// </para>
/// </summary>
public class WriteCommandOptionsPlumbingTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public WriteCommandOptionsPlumbingTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);

        // Promote VIEW to TABLE outside any manually-managed transaction: TablePromoter opens its
        // own, and DuckDB refuses a nested one.
        _db.Delete<SalesRecord>(r => r.Id == -1);
    }

    public void Dispose() => _db.Dispose();

    private static SalesRecord NewRecord(int id) => new()
    {
        Id = id,
        Region = "Antarctica",
        ProductName = "Ice",
        Revenue = 1m,
        Quantity = 1,
        Date = new DateTime(2026, 1, 1),
    };

    private void WithWrapper(Action<CommandOptions> write)
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(
            () => write(CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    private async Task WithWrapperAsync(Func<CommandOptions, Task> write)
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => write(CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public void Delete_ForwardsOptionsToTheCommand()
        => WithWrapper(o => _db.Delete<SalesRecord>(r => r.Id == 90201, o));

    [Fact]
    public Task DeleteAsync_ForwardsOptionsToTheCommand()
        => WithWrapperAsync(o => _db.DeleteAsync<SalesRecord>(r => r.Id == 90201, o).AsTask());

    [Fact]
    public void Update_ForwardsOptionsToTheCommand()
        => WithWrapper(o => _db.Update<SalesRecord>(r => r.Id == 90201, r => r.Region!, "Arctic", o));

    [Fact]
    public Task UpdateAsync_ForwardsOptionsToTheCommand()
        => WithWrapperAsync(o => _db.UpdateAsync<SalesRecord>(r => r.Id == 90201, r => r.Region!, "Arctic", o).AsTask());

    [Fact]
    public void Insert_ForwardsOptionsToTheCommand()
        => WithWrapper(o => _db.Insert(NewRecord(90202), o));

    [Fact]
    public Task InsertAsync_ForwardsOptionsToTheCommand()
        => WithWrapperAsync(o => _db.InsertAsync(NewRecord(90203), o).AsTask());

    [Fact]
    public void InsertMany_ForwardsOptionsToTheCommand()
        => WithWrapper(o => _db.Insert([NewRecord(90204), NewRecord(90205)], o));

    [Fact]
    public Task InsertManyAsync_ForwardsOptionsToTheCommand()
        => WithWrapperAsync(o => _db.InsertAsync([NewRecord(90206), NewRecord(90207)], o).AsTask());

    // ------------------------------------------------------------------
    // Controls: a real transaction still works through the same overloads.
    // ------------------------------------------------------------------

    [Fact]
    public void Insert_WithARealTransaction_StillWrites()
    {
        using var tx = _db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        Assert.Equal(1, _db.Insert(NewRecord(90208), options));
        Assert.Single(_db.Query<SalesRecord>("SELECT * FROM \"sales\" WHERE \"id\" = 90208", options));

        tx.Rollback();
    }

    [Fact]
    public void Delete_WithATimeout_StillDeletes()
    {
        _db.Insert(NewRecord(90209));

        Assert.Equal(1, _db.Delete<SalesRecord>(r => r.Id == 90209, CommandOptions.WithTimeout(30)));
    }
}
