using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R32-009: the raw-SQL <c>Query&lt;T&gt;</c> reads had no <c>CommandOptions</c> overload, so
/// they could not be enlisted in the transaction their <c>Insert</c>/<c>Update</c>/<c>Delete</c>
/// neighbours were given.
/// </summary>
/// <remarks>
/// The non-DbTransaction tests are the ones that pin the plumbing. DuckDB, like SQLite, binds a
/// command to the connection's open transaction implicitly, so a rollback round-trip stays green
/// even if the option never reaches the command - only the validator's ArgumentException proves
/// <c>ApplyOptions</c> ran on this path.
/// </remarks>
public class QueryCommandOptionsTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public QueryCommandOptionsTests()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(csvPath);
        _db = new DuckDb(options);

        // Force promotion from VIEW to TABLE outside of any manually-managed transaction:
        // TablePromoter opens its own transaction, and DuckDB refuses a nested one.
        _db.Delete<SalesRecord>(r => r.Id == -1);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Query_WithNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Query<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTransaction(nonDbTransaction)));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public void Query_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Query<SalesRecord>(
                "SELECT * FROM \"sales\" WHERE \"region\" = $region",
                CommandOptions.WithTransaction(nonDbTransaction),
                ("region", "Northeast")));
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public async Task QueryAsync_WithNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryAsync<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTransaction(nonDbTransaction)).AsTask());
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public async Task QueryAsync_WithParametersAndNonDbTransaction_ThrowsArgumentException()
    {
        using var realTransaction = _db.Connection.BeginTransaction();
        using var nonDbTransaction = new IDbTransactionWrapper(realTransaction);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryAsync<SalesRecord>(
                "SELECT * FROM \"sales\" WHERE \"region\" = $region",
                [("region", "Northeast")],
                CommandOptions.WithTransaction(nonDbTransaction)).AsTask());
        Assert.Contains("DbTransaction", ex.Message);

        realTransaction.Rollback();
    }

    [Fact]
    public void Query_WithTransactionOptions_ReadsRowsWrittenInThatTransaction()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        _db.Insert(
            new SalesRecord { Id = 90001, Region = "Antarctica", ProductName = "Ice", Revenue = 1m, Quantity = 1, Date = new DateTime(2026, 1, 1) },
            options);

        var results = _db.Query<SalesRecord>(
            "SELECT * FROM \"sales\" WHERE \"region\" = $region",
            options,
            ("region", "Antarctica"));

        Assert.Single(results);

        tx.Rollback();
    }

    [Fact]
    public async Task QueryAsync_WithTransactionOptions_ReadsRowsWrittenInThatTransaction()
    {
        using var tx = (DuckDBTransaction)_db.Connection.BeginTransaction();
        var options = CommandOptions.WithTransaction(tx);

        await _db.InsertAsync(
            new SalesRecord { Id = 90002, Region = "Arctic", ProductName = "Ice", Revenue = 1m, Quantity = 1, Date = new DateTime(2026, 1, 1) },
            options);

        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM \"sales\" WHERE \"region\" = $region",
            [("region", "Arctic")],
            options);

        Assert.Single(results);

        tx.Rollback();
    }

    [Fact]
    public void Query_WithTimeoutOptions_StillReturnsResults()
    {
        var results = _db.Query<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task QueryAsync_WithTimeoutOptions_StillReturnsResults()
    {
        var results = await _db.QueryAsync<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    [Fact]
    public void Query_WithOptions_ThroughIFlatFileInterface_ReturnsResults()
    {
        IFlatFile flatFile = _db;

        var results = flatFile.Query<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task QueryAsync_WithOptions_ThroughIFlatFileInterface_ReturnsResults()
    {
        IFlatFile flatFile = _db;

        var results = await flatFile.QueryAsync<SalesRecord>("SELECT * FROM \"sales\"", CommandOptions.WithTimeout(30));

        Assert.NotEmpty(results);
    }

    [Fact]
    public void Query_WithOptions_NullSql_Throws()
        => Assert.Throws<ArgumentNullException>(() => _db.Query<SalesRecord>(null!, CommandOptions.WithTimeout(30)));

    [Fact]
    public void Query_WithOptions_WhitespaceSql_Throws()
        => Assert.Throws<ArgumentException>(() => _db.Query<SalesRecord>("   ", CommandOptions.WithTimeout(30)));

    [Fact]
    public void Query_WithOptionsAndParameters_WhitespaceSql_Throws()
        => Assert.Throws<ArgumentException>(() =>
            _db.Query<SalesRecord>("   ", CommandOptions.WithTimeout(30), ("region", "Northeast")));

    [Fact]
    public async Task QueryAsync_WithOptions_NullSql_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _db.QueryAsync<SalesRecord>(null!, CommandOptions.WithTimeout(30)).AsTask());

    [Fact]
    public async Task QueryAsync_WithOptionsAndParameters_WhitespaceSql_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryAsync<SalesRecord>("   ", [("region", "Northeast")], CommandOptions.WithTimeout(30)).AsTask());
}
