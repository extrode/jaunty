using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

namespace Jaunty.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks querying TPC-H data (1.5M orders, 6M line items) at scale.
/// Requires prior --setup to import data into a file-based SQLite database.
/// </summary>
public class TpcQueryBenchmarks
{
    private DbConnection _connection = null!;
    private const string OrdersQuery = "SELECT o_orderkey, o_custkey, o_orderstatus, o_totalprice, o_orderdate, o_orderpriority, o_clerk, o_shippriority, o_comment FROM orders LIMIT @Limit";
    private const string OrdersQueryDapper = "SELECT o_orderkey, o_custkey, o_orderstatus, o_totalprice, o_orderdate, o_orderpriority, o_clerk, o_shippriority, o_comment FROM orders LIMIT @Limit";

    [Params(100, 1_000, 10_000)]
    public int Limit { get; set; }

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

    // --- ADO.NET (hand-coded baseline) ---

    [Benchmark(Description = "ADO.NET (hand-coded)", Baseline = true)]
    public List<DapperOrder> AdoNet_Query()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT o_orderkey, o_custkey, o_orderstatus, o_totalprice, o_orderdate, o_orderpriority, o_clerk, o_shippriority, o_comment FROM orders LIMIT @Limit";
        var param = cmd.CreateParameter();
        param.ParameterName = "@Limit";
        param.Value = Limit;
        cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        var results = new List<DapperOrder>();
        while (reader.Read())
        {
            results.Add(new DapperOrder
            {
                o_orderkey = reader.GetInt32(0),
                o_custkey = reader.GetInt32(1),
                o_orderstatus = reader.GetString(2),
                o_totalprice = reader.GetDecimal(3),
                o_orderdate = reader.GetString(4),
                o_orderpriority = reader.GetString(5),
                o_clerk = reader.GetString(6),
                o_shippriority = reader.GetInt32(7),
                o_comment = reader.GetString(8)
            });
        }
        return results;
    }

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty Query<T>")]
    public List<TpcOrder> Jaunty_Query()
    {
        return _connection.Query<TpcOrder>(OrdersQuery, new { Limit });
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper Query<T>")]
    public List<DapperOrder> Dapper_Query()
    {
        return SqlMapper.Query<DapperOrder>(_connection, OrdersQueryDapper, new { Limit }).AsList();
    }
}
