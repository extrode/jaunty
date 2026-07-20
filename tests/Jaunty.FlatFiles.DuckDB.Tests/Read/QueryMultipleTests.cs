using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Regression tests for <see cref="DuckDb.QueryMultiple(string)"/> /
/// <see cref="DuckDb.QueryMultipleAsync(string, CancellationToken)"/>: the underlying
/// <c>DuckDBCommand</c> must be disposed alongside the <see cref="GridReader"/> (not leaked),
/// including on the exception path if <c>ExecuteReader</c> itself throws.
/// </summary>
public class QueryMultipleTests : IDisposable
{
    private readonly DuckDb _db;
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    public QueryMultipleTests()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void QueryMultiple_TwoResultSets_ReadsBothInOrder()
    {
        using var grid = _db.QueryMultiple(
            "SELECT * FROM \"sales\"; SELECT COUNT(*) AS cnt FROM \"sales\"");

        var rows = grid.Read<SalesRecord>();
        var count = grid.ReadFirst<CountRow>();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count, count.Cnt);
    }

    [Fact]
    public void QueryMultiple_WithParameters_BindsByName()
    {
        using var grid = _db.QueryMultiple(
            "SELECT * FROM \"sales\" WHERE \"region\" = $Region",
            new { Region = "Northeast" });

        var rows = grid.Read<SalesRecord>();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal("Northeast", r.Region));
    }

    [Fact]
    public void QueryMultiple_InvalidSql_DoesNotLeakCommand()
    {
        // The command creation/ExecuteReader must be wrapped so a failure here disposes the
        // command instead of leaking it (there's no GridReader to take ownership on this path).
        Assert.ThrowsAny<Exception>(() => _db.QueryMultiple("SELECT * FROM \"this_table_does_not_exist\""));

        // The connection must remain usable afterward — proves the failed command didn't corrupt state.
        using var grid = _db.QueryMultiple("SELECT 1 AS cnt");
        var result = grid.ReadFirst<CountRow>();
        Assert.Equal(1, result.Cnt);
    }

    [Fact]
    public async Task QueryMultipleAsync_TwoResultSets_ReadsBothInOrder()
    {
        await using var grid = await _db.QueryMultipleAsync(
            "SELECT * FROM \"sales\"; SELECT COUNT(*) AS cnt FROM \"sales\"");

        var rows = grid.Read<SalesRecord>();
        var count = grid.ReadFirst<CountRow>();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count, count.Cnt);
    }

    [Fact]
    public async Task QueryMultipleAsync_InvalidSql_DoesNotLeakCommand()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await _db.QueryMultipleAsync("SELECT * FROM \"this_table_does_not_exist\""));

        await using var grid = await _db.QueryMultipleAsync("SELECT 1 AS cnt");
        var result = grid.ReadFirst<CountRow>();
        Assert.Equal(1, result.Cnt);
    }

    private sealed class CountRow
    {
        public long Cnt { get; set; }
    }
}
