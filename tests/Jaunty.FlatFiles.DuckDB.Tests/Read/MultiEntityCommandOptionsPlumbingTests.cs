using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-260. The four <c>CommandOptions&lt;(T1, T2)&gt;</c> overloads were called by
/// <c>MultiEntityQueryTests</c>, but every one of those tests passed a timeout and asserted only
/// that rows still came back - which proves nothing, because DuckDB binds a command to the
/// connection's open transaction implicitly, so the assertion stays green even if the option never
/// reaches the command. Only the transaction validator's <see cref="ArgumentException"/> shows that
/// <c>ApplyOptions</c> ran on this path, which is the same shape <c>QueryCommandOptionsTests</c>
/// uses for the sibling gap AUD-R32-009 closed.
/// </summary>
public class MultiEntityCommandOptionsPlumbingTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public MultiEntityCommandOptionsPlumbingTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);

        // As in QueryCommandOptionsTests: force promotion outside any manually-managed
        // transaction, since TablePromoter opens its own and DuckDB refuses a nested one.
        _db.Delete<SalesRecord>(r => r.Id == -1);
    }

    public void Dispose() => _db.Dispose();

    private sealed class Left
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Right
    {
        public int Amount { get; set; }
    }

    private const string Sql = "SELECT 1 AS Id, 'x' AS Name, 2 AS Amount";
    private const string ParameterisedSql = "SELECT 1 AS Id, 'x' AS Name, $Amount AS Amount";

    private CommandOptions<(Left, Right)> BadTransaction(out IDbTransactionWrapper wrapper, out IDisposable real)
    {
        var realTransaction = _db.Connection.BeginTransaction();
        real = realTransaction;
        wrapper = new IDbTransactionWrapper(realTransaction);

        return new CommandOptions<(Left, Right)>(transaction: wrapper);
    }

    [Fact]
    public void QueryMultiEntity_WithNonDbTransaction_ThrowsArgumentException()
    {
        CommandOptions<(Left, Right)> options = BadTransaction(out IDbTransactionWrapper wrapper, out IDisposable real);

        var ex = Assert.Throws<ArgumentException>(() => _db.QueryMultiEntity<Left, Right>(Sql, options));

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);

        wrapper.Dispose();
        real.Dispose();
    }

    [Fact]
    public void QueryMultiEntity_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        CommandOptions<(Left, Right)> options = BadTransaction(out IDbTransactionWrapper wrapper, out IDisposable real);

        var ex = Assert.Throws<ArgumentException>(
            () => _db.QueryMultiEntity<Left, Right>(ParameterisedSql, new { Amount = 2 }, options));

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);

        wrapper.Dispose();
        real.Dispose();
    }

    [Fact]
    public async Task QueryMultiEntityAsync_WithNonDbTransaction_ThrowsArgumentException()
    {
        CommandOptions<(Left, Right)> options = BadTransaction(out IDbTransactionWrapper wrapper, out IDisposable real);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _db.QueryMultiEntityAsync<Left, Right>(Sql, options).AsTask());

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);

        wrapper.Dispose();
        real.Dispose();
    }

    [Fact]
    public async Task QueryMultiEntityAsync_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        CommandOptions<(Left, Right)> options = BadTransaction(out IDbTransactionWrapper wrapper, out IDisposable real);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _db.QueryMultiEntityAsync<Left, Right>(ParameterisedSql, new { Amount = 2 }, options).AsTask());

        Assert.Contains("DbTransaction", ex.Message, StringComparison.Ordinal);

        wrapper.Dispose();
        real.Dispose();
    }
}
