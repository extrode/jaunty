using Jaunty.Core;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R35-256, AUD-R35-258 and AUD-R35-259. The parameterised <c>QueryMultiple</c> overloads'
/// argument guards had no test, the async parameterised overload had no test at all - so
/// <c>ExecuteQueryMultipleDirectAsync</c>'s binding branch never executed in the suite - and a
/// parameters object carrying an indexer or a write-only property reached
/// <c>PropertyInfo.GetValue</c> and threw <c>TargetParameterCountException</c> /
/// <c>ArgumentException</c>, naming nothing.
/// </summary>
public class QueryMultipleGuardTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public QueryMultipleGuardTests()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(Path.Combine(DataDir, "csv", "sales.csv"));
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    private const string Sql = "SELECT * FROM \"sales\" WHERE \"region\" = $Region";

    private sealed class WithIndexer
    {
        public string Region { get; set; } = "Northeast";
        public string this[int index] => index.ToString();
    }

    private sealed class WithWriteOnly
    {
        public string Region { get; set; } = "Northeast";

        private string _ignored = "";
        public string WriteOnly { set => _ignored = value; }

        public string Read() => _ignored;
    }

    [Fact]
    public void QueryMultiple_NullSql_Throws()
        => Assert.Throws<ArgumentNullException>(() => _db.QueryMultiple(null!, new { Region = "Northeast" }));

    [Fact]
    public void QueryMultiple_WhitespaceSql_Throws()
        => Assert.Throws<ArgumentException>(() => _db.QueryMultiple("   ", new { Region = "Northeast" }));

    /// <summary>
    /// Measured, not assumed: a null parameters object is the same as passing none -
    /// <c>ExecuteQueryMultipleDirect</c> skips the binding loop entirely. So SQL that names a
    /// placeholder fails from DuckDB's prepared statement rather than from a Jaunty guard, and SQL
    /// that names none runs. Pinned so the "null means none" reading cannot drift into a silent
    /// empty result.
    /// </summary>
    [Fact]
    public void QueryMultiple_NullParameters_IsTreatedAsNone()
    {
        Assert.Throws<InvalidOperationException>(() => _db.QueryMultiple(Sql, (object)null!));

        using GridReader grid = _db.QueryMultiple("SELECT * FROM \"sales\"", (object)null!);

        Assert.NotEmpty(grid.Read<SalesRecord>());
    }

    [Fact]
    public async Task QueryMultipleAsync_NullSql_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _db.QueryMultipleAsync(null!, new { Region = "Northeast" }));

    [Fact]
    public async Task QueryMultipleAsync_WhitespaceSql_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(
            async () => await _db.QueryMultipleAsync("   ", new { Region = "Northeast" }));

    [Fact]
    public async Task QueryMultipleAsync_WithParameters_BindsByName()
    {
        using GridReader grid = await _db.QueryMultipleAsync(Sql, new { Region = "Northeast" });

        List<SalesRecord> rows = grid.Read<SalesRecord>();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public async Task QueryMultipleAsync_WithParameters_FiltersRatherThanReturningEverything()
    {
        using GridReader all = await _db.QueryMultipleAsync("SELECT * FROM \"sales\"");
        int total = all.Read<SalesRecord>().Count;

        using GridReader filtered = await _db.QueryMultipleAsync(Sql, new { Region = "Northeast" });
        int matched = filtered.Read<SalesRecord>().Count;

        Assert.InRange(matched, 1, total - 1);
    }

    [Fact]
    public void AnIndexerOnTheParametersObject_IsSkippedRatherThanBound()
    {
        using GridReader grid = _db.QueryMultiple(Sql, new WithIndexer());

        Assert.NotEmpty(grid.Read<SalesRecord>());
    }

    [Fact]
    public async Task AnIndexerOnTheParametersObject_IsSkippedOnTheAsyncPathToo()
    {
        using GridReader grid = await _db.QueryMultipleAsync(Sql, new WithIndexer());

        Assert.NotEmpty(grid.Read<SalesRecord>());
    }

    [Fact]
    public void AWriteOnlyPropertyOnTheParametersObject_IsSkipped()
    {
        using GridReader grid = _db.QueryMultiple(Sql, new WithWriteOnly());

        Assert.NotEmpty(grid.Read<SalesRecord>());
    }
}
