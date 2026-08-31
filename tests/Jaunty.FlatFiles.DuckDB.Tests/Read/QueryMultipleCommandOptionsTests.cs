using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-076. None of the four <c>QueryMultiple</c>/<c>QueryMultipleAsync</c> entry points had a
/// <see cref="CommandOptions"/> overload, and neither <c>ExecuteQueryMultipleDirect</c> nor its
/// async twin called <c>NonQueryExecutor.ApplyOptions</c> - so a multi-result-set read could not be
/// enlisted in the transaction or given the timeout its <c>Insert</c>/<c>Update</c>/<c>Delete</c>
/// neighbours were given. <c>QueryMultiEntity</c> had this gap closed in AUD-R26-068 and
/// <c>Query&lt;T&gt;</c> in AUD-R32-009, both for the identical stated reason.
/// </summary>
/// <remarks>
/// The <c>IDbTransactionWrapper</c> tests are the ones that pin the plumbing: DuckDB binds a command
/// to the connection's open transaction implicitly, so a rollback round-trip stays green even when
/// the option never reaches the command. Only the validator's <see cref="ArgumentException"/>
/// proves <c>ApplyOptions</c> ran on this path.
/// </remarks>
public class QueryMultipleCommandOptionsTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public QueryMultipleCommandOptionsTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);

        // Force promotion from VIEW to TABLE outside any manually-managed transaction:
        // TablePromoter opens its own, and DuckDB refuses a nested one.
        _db.Delete<SalesRecord>(r => r.Id == -1);
    }

    public void Dispose() => _db.Dispose();

    private const string TwoResultSets = "SELECT * FROM \"sales\"; SELECT * FROM \"sales\"";

    [Fact]
    public void QueryMultiple_WithNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _db.QueryMultiple(TwoResultSets, CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public void QueryMultiple_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _db.QueryMultiple(
                "SELECT * FROM \"sales\" WHERE \"region\" = $Region; SELECT * FROM \"sales\"",
                new { Region = "Northeast" },
                CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public async Task QueryMultipleAsync_WithNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryMultipleAsync(TwoResultSets, CommandOptions.WithTransaction(nonDbTransaction)).AsTask());
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public async Task QueryMultipleAsync_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryMultipleAsync(
                "SELECT * FROM \"sales\" WHERE \"region\" = $Region; SELECT * FROM \"sales\"",
                new { Region = "Northeast" },
                CommandOptions.WithTransaction(nonDbTransaction)).AsTask());
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public void QueryMultiple_WithTransactionOptions_ReadsRowsWrittenInThatTransaction()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        _db.Insert(
            new SalesRecord { Id = 90101, Region = "Antarctica", ProductName = "Ice", Revenue = 1m, Quantity = 1, Date = new DateTime(2026, 1, 1) },
            options);

        using (GridReader grid = _db.QueryMultiple(
            "SELECT * FROM \"sales\" WHERE \"region\" = $Region; SELECT * FROM \"sales\"",
            new { Region = "Antarctica" },
            options))
        {
            Assert.Single(grid.Read<SalesRecord>());
            Assert.NotEmpty(grid.Read<SalesRecord>());
        }

        tx.Rollback();
    }

    [Fact]
    public void QueryMultiple_WithATimeout_StillReturnsTheResultSets()
    {
        using GridReader grid = _db.QueryMultiple(TwoResultSets, CommandOptions.WithTimeout(30));

        Assert.NotEmpty(grid.Read<SalesRecord>());
        Assert.NotEmpty(grid.Read<SalesRecord>());
    }

    // ------------------------------------------------------------------
    // Controls: the options-free overloads are unchanged.
    // ------------------------------------------------------------------

    [Fact]
    public void QueryMultiple_WithoutOptions_StillReadsBothResultSets()
    {
        using GridReader grid = _db.QueryMultiple(TwoResultSets);

        Assert.NotEmpty(grid.Read<SalesRecord>());
        Assert.NotEmpty(grid.Read<SalesRecord>());
    }

    [Fact]
    public async Task QueryMultipleAsync_WithoutOptions_StillReadsBothResultSets()
    {
        await using GridReader grid = await _db.QueryMultipleAsync(TwoResultSets);

        Assert.NotEmpty(grid.Read<SalesRecord>());
        Assert.NotEmpty(grid.Read<SalesRecord>());
    }
}
