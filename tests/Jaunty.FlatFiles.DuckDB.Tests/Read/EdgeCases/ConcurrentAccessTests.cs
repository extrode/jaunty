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

    /// <summary>One DuckDb (and therefore one connection) per task: ADO.NET
    /// connections are single-threaded by contract, so concurrency is
    /// exercised at the flat-file level, never by sharing a connection.
    /// (Sharing one connection across tasks failed intermittently on CI
    /// with 'DuckDB execution failed' - a test bug, not a Jaunty one.)</summary>
    private DuckDb CreateDb()
    {
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(_csvPath);
        return new DuckDb(options);
    }

    [Fact]
    public async Task ConcurrentReads_DoNotCorruptResults()
    {
        // Get expected count
        var expected = _db.Connection.From<SalesRecord>().Select().Count;

        // Run 10 concurrent queries, each on its own connection
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            using var db = CreateDb();
            var results = db.Connection.Query<SalesRecord>(
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
            Task.Run(() =>
            {
                using var db = CreateDb();
                return db.Connection.Query<SalesRecord>("SELECT * FROM \"sales\"").Count;
            }),
            Task.Run(() =>
            {
                using var db = CreateDb();
                using var cmd = db.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM \"sales\"";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }),
            Task.Run(() =>
            {
                using var db = CreateDb();
                return db.Connection.Query<SalesRecord>(
                    "SELECT * FROM \"sales\" WHERE \"Quantity\" > 0").Count;
            })
        };

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r > 0, "Each concurrent query should return results"));
    }
}