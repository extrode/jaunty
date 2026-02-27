using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryFirstBenchmarks
{
    private DbConnection _connection = null!;

    [Params(DatabaseProvider.Sqlite)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

        RepoDb.GlobalConfiguration.Setup().UseSqlite();
        RepoDb.TypeMapper.Add(typeof(bool), System.Data.DbType.Int64);

        _connection = DatabaseSetup.CreateConnection(Provider);
        _connection.Open();
        DatabaseSetup.CreateSchema(_connection, Provider);
        DatabaseSetup.SeedData(_connection, Provider, 100);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty QueryFirst")]
    public JauntyProduct Jaunty_QueryFirst()
    {
        return _connection.QueryFirst<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = @Id",
            new { Id = 1 });
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper QueryFirst", Baseline = true)]
    public DapperProduct Dapper_QueryFirst()
    {
        return _connection.QueryFirst<DapperProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = @Id",
            new { Id = 1 });
    }

    // --- RepoDb ---

    [Benchmark(Description = "RepoDb Query (first)")]
    public RepoDbProduct RepoDb_QueryFirst()
    {
        return RepoDb.DbConnectionExtension.Query<RepoDbProduct>(_connection, 1).First();
    }
}
