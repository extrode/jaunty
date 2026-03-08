using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;

namespace Jaunty.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks aggregation queries against TPC-H data (1.5M orders, 6M line items).
/// Tests scalar/aggregation query performance at scale.
/// Requires prior --setup to import data into a file-based SQLite database.
/// </summary>
public class TpcAggregationBenchmarks
{
    private DbConnection _connection = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = TpcDataImporter.CreateSqliteFileConnection();
        _connection.Open();

        // Verify data exists
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM orders";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        if (count == 0)
            throw new InvalidOperationException(
                "No TPC-H data found. Run: dotnet run --project benchmarks/Jaunty.Benchmarks -- --setup --provider Sqlite");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // =============================================
    // COUNT(*) on orders (1.5M rows)
    // =============================================

    [Benchmark(Description = "ADO.NET COUNT(*) orders", Baseline = true)]
    public long AdoNet_CountOrders()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM orders";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Benchmark(Description = "Jaunty COUNT(*) orders")]
    public long Jaunty_CountOrders()
    {
        return _connection.QueryScalar<long>("SELECT COUNT(*) FROM orders");
    }

    [Benchmark(Description = "Dapper COUNT(*) orders")]
    public long Dapper_CountOrders()
    {
        return SqlMapper.ExecuteScalar<long>(_connection, "SELECT COUNT(*) FROM orders");
    }

    // =============================================
    // SUM(o_totalprice) on orders
    // =============================================

    [Benchmark(Description = "ADO.NET SUM orders")]
    public decimal AdoNet_SumOrders()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT SUM(o_totalprice) FROM orders";
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    [Benchmark(Description = "Jaunty SUM orders")]
    public decimal Jaunty_SumOrders()
    {
        return _connection.QueryScalar<decimal>("SELECT SUM(o_totalprice) FROM orders");
    }

    [Benchmark(Description = "Dapper SUM orders")]
    public decimal Dapper_SumOrders()
    {
        return SqlMapper.ExecuteScalar<decimal>(_connection, "SELECT SUM(o_totalprice) FROM orders");
    }

    // =============================================
    // GROUP BY with COUNT on lineitem (6M rows)
    // =============================================

    [Benchmark(Description = "ADO.NET GROUP BY lineitem")]
    public List<(string flag, long count)> AdoNet_GroupByLineitem()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT l_returnflag, COUNT(*) as cnt FROM lineitem GROUP BY l_returnflag ORDER BY l_returnflag";
        using var reader = cmd.ExecuteReader();
        var results = new List<(string, long)>();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt64(1)));
        }
        return results;
    }

    [Benchmark(Description = "Jaunty GROUP BY lineitem")]
    public List<LineitemGroupResult> Jaunty_GroupByLineitem()
    {
        return _connection.Query<LineitemGroupResult>(
            "SELECT l_returnflag AS ReturnFlag, COUNT(*) as Count FROM lineitem GROUP BY l_returnflag ORDER BY l_returnflag");
    }

    [Benchmark(Description = "Dapper GROUP BY lineitem")]
    public List<LineitemGroupResult> Dapper_GroupByLineitem()
    {
        return SqlMapper.Query<LineitemGroupResult>(_connection,
            "SELECT l_returnflag AS ReturnFlag, COUNT(*) as Count FROM lineitem GROUP BY l_returnflag ORDER BY l_returnflag")
            .AsList();
    }
}

/// <summary>
/// Result type for GROUP BY aggregation queries.
/// </summary>
public class LineitemGroupResult
{
    public string ReturnFlag { get; set; } = null!;
    public long Count { get; set; }
}