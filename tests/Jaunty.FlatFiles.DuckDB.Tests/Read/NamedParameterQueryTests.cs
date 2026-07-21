using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Regression tests for <see cref="DuckDb.Query{T}(string, (string Name, object? Value)[])"/> and
/// <see cref="DuckDb.QueryAsync{T}(string, IEnumerable{(string Name, object? Value)}, CancellationToken)"/>:
/// the tuple's <c>Name</c> must actually be bound as the ADO parameter name so <c>$name</c>-style
/// placeholders resolve by name, not merely by declaration order.
/// </summary>
public class NamedParameterQueryTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public NamedParameterQueryTests()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Query_NamedParameter_BindsByNameNotPosition()
    {
        var results = _db.Query<SalesRecord>(
            "SELECT * FROM \"sales\" WHERE \"region\" = $region",
            ("region", "Northeast"));

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public void Query_MultipleNamedParameters_OrderIndependent()
    {
        // Passing the tuples in reverse SQL-usage order proves binding is by name, not position:
        // if it were positional, $minRevenue would receive "Northeast" and fail/mismatch.
        var results = _db.Query<SalesRecord>(
            "SELECT * FROM \"sales\" WHERE \"region\" = $region AND \"revenue\" > $minRevenue",
            ("minRevenue", 0m),
            ("region", "Northeast"));

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public async Task QueryAsync_NamedParameter_BindsByNameNotPosition()
    {
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM \"sales\" WHERE \"region\" = $region",
            [("region", "Northeast")]);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Northeast", r.Region));
    }

    // ==========================================
    // AUD-R14 batch-7: DuckDb.Query/QueryAsync null/whitespace sql validation
    // ==========================================

    [Fact]
    public void Query_NullSql_Throws()
        => Assert.Throws<ArgumentNullException>(() => _db.Query<SalesRecord>(null!));

    [Fact]
    public void Query_WhitespaceSql_Throws()
        => Assert.Throws<ArgumentException>(() => _db.Query<SalesRecord>("   "));

    [Fact]
    public void Query_WithParameters_NullSql_Throws()
        => Assert.Throws<ArgumentNullException>(() => _db.Query<SalesRecord>(null!, ("region", "Northeast")));

    [Fact]
    public void Query_WithParameters_WhitespaceSql_Throws()
        => Assert.Throws<ArgumentException>(() => _db.Query<SalesRecord>("   ", ("region", "Northeast")));

    [Fact]
    public async Task QueryAsync_NullSql_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _db.QueryAsync<SalesRecord>(null!).AsTask());

    [Fact]
    public async Task QueryAsync_WhitespaceSql_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() => _db.QueryAsync<SalesRecord>("   ").AsTask());

    [Fact]
    public async Task QueryAsync_WithParameters_NullSql_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _db.QueryAsync<SalesRecord>(null!, [("region", "Northeast")]).AsTask());

    [Fact]
    public async Task QueryAsync_WithParameters_WhitespaceSql_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() =>
            _db.QueryAsync<SalesRecord>("   ", [("region", "Northeast")]).AsTask());
}
