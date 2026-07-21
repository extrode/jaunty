namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Regression tests for <see cref="DuckDb.QueryMultiEntity{T1, T2}(string)"/> and its
/// parameterized/async overloads - the DuckDB-specific public wrappers around
/// <c>IDbConnection.Query{T1,T2}</c>/<c>QueryAsync{T1,T2}</c>. These were previously untested
/// against a real DuckDb connection (only the generic core implementation was covered elsewhere).
/// </summary>
public class MultiEntityQueryTests : IDisposable
{
    private readonly DuckDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void QueryMultiEntity_TwoTypes_MapsColumnsByName()
    {
        List<(Left, Right)> results = _db.QueryMultiEntity<Left, Right>(
            "SELECT 1 AS Id, 'Alice' AS Name, 100 AS Amount");

        (Left left, Right right) = Assert.Single(results);
        Assert.Equal(1, left.Id);
        Assert.Equal("Alice", left.Name);
        Assert.Equal(100, right.Amount);
    }

    [Fact]
    public void QueryMultiEntity_WithParameters_BindsByName()
    {
        List<(Left, Right)> results = _db.QueryMultiEntity<Left, Right>(
            "SELECT 2 AS Id, 'Bob' AS Name, $Amount AS Amount",
            new { Amount = 250 });

        (Left left, Right right) = Assert.Single(results);
        Assert.Equal(2, left.Id);
        Assert.Equal("Bob", left.Name);
        Assert.Equal(250, right.Amount);
    }

    [Fact]
    public async Task QueryMultiEntityAsync_TwoTypes_MapsColumnsByName()
    {
        List<(Left, Right)> results = await _db.QueryMultiEntityAsync<Left, Right>(
            "SELECT 3 AS Id, 'Carol' AS Name, 300 AS Amount");

        (Left left, Right right) = Assert.Single(results);
        Assert.Equal(3, left.Id);
        Assert.Equal("Carol", left.Name);
        Assert.Equal(300, right.Amount);
    }

    [Fact]
    public async Task QueryMultiEntityAsync_WithParameters_BindsByName()
    {
        List<(Left, Right)> results = await _db.QueryMultiEntityAsync<Left, Right>(
            "SELECT 4 AS Id, 'Dave' AS Name, $Amount AS Amount",
            new { Amount = 400 },
            CancellationToken.None);

        (Left left, Right right) = Assert.Single(results);
        Assert.Equal(4, left.Id);
        Assert.Equal("Dave", left.Name);
        Assert.Equal(400, right.Amount);
    }

    private sealed class Left
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Right
    {
        public int Amount { get; set; }
    }
}
