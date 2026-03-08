using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read.EdgeCases;

/// <summary>
/// Tests for concurrent access patterns and thread safety.
/// </summary>
public class ConcurrentAccessTests : IDisposable
{
    private readonly DuckDb _db;
    private readonly string _csvPath;

    public ConcurrentAccessTests()
    {
        _csvPath = Path.Combine(AppContext.BaseDirectory, "data", "csv", "sales.csv");

        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task ConcurrentReads_DoNotCorruptResults()
    {
        // Get expected count
        var expected = _db.Connection.From<SalesRecord>().Select().Count;

        // Run 10 concurrent queries
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var results = _db.Connection.Query<SalesRecord>(
                "SELECT * FROM \"sales\"");
            return results.Count;
        }));

        var counts = await Task.WhenAll(tasks);

        // All concurrent reads should return the same count
        Assert.All(counts, c => Assert.Equal(expected, c));
    }

    [Fact]
    public async Task ConcurrentReads_DifferentQueries_Succeed()
    {
        var tasks = new List<Task<int>>
        {
            Task.Run(() => _db.Connection.Query<SalesRecord>("SELECT * FROM \"sales\"").Count),
            Task.Run(() =>
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM \"sales\"";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }),
            Task.Run(() => _db.Connection.Query<SalesRecord>(
                "SELECT * FROM \"sales\" WHERE \"Quantity\" > 0").Count)
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r > 0, "Each concurrent query should return results"));
    }
}