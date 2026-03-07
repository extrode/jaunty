using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Performance;

/// <summary>
/// Performance test: query a 100K-row CSV to verify scalability.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly string _largeCsvPath;
    private readonly DuckDb _db;

    public PerformanceTests()
    {
        // Generate a 100K-row CSV using DuckDB
        _largeCsvPath = Path.Combine(Path.GetTempPath(), $"jaunty_perf_test_{Guid.NewGuid():N}.csv");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT
                    i AS Id,
                    'Product_' || (i % 100)::VARCHAR AS product_name,
                    ROUND((random() * 100000)::DECIMAL(10,2), 2) AS Revenue,
                    (random() * 1000)::INTEGER AS Quantity,
                    TIMESTAMP '2024-01-01' + INTERVAL (random() * 365) DAY AS Date,
                    CASE (random() * 4)::INTEGER
                        WHEN 0 THEN 'Northeast'
                        WHEN 1 THEN 'Southeast'
                        WHEN 2 THEN 'Midwest'
                        ELSE 'West'
                    END AS region
                FROM range(100000) t(i)
            ) TO '{_largeCsvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(_largeCsvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_largeCsvPath); } catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void LargeFile_100K_Rows_QueriesSuccessfully()
    {
        var count = _db.Connection.From<SalesRecord>().Count();
        Assert.Equal(100000, count);
    }

    [Fact]
    public void LargeFile_100K_WithFilter_ReturnsSubset()
    {
        var results = _db.Connection.From<SalesRecord>()
            .Where(s => s.Revenue > 50000m)
            .OrderByDescending(s => s.Revenue)
            .Take(100)
            .Select();

        Assert.True(results.Count <= 100);
        Assert.All(results, r => Assert.True(r.Revenue > 50000m));
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
    }

    [Fact]
    public void LargeFile_100K_Aggregate_Works()
    {
        // Use raw SQL because DuckDB SUM returns HUGEINT which doesn't implement IConvertible
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT SUM(\"Quantity\") FROM \"sales\"";
        var result = cmd.ExecuteScalar();
        Assert.NotNull(result);
        var sum = Convert.ToInt64(result!.ToString());
        Assert.True(sum > 0);
    }
}
