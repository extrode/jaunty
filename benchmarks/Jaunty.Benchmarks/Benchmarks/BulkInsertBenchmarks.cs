using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class BulkInsertBenchmarks
{
    private DbConnection _connection = null!;
    private List<JauntyProduct> _jauntyProducts = null!;
    private List<RepoDbProduct> _repoDbProducts = null!;

    [Params(100, 1000)]
    public int BatchSize { get; set; }

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

        _jauntyProducts = Enumerable.Range(0, BatchSize).Select(i => new JauntyProduct
        {
            ProductName = $"Bulk Product {i}",
            UnitPrice = 10.00m + (i % 100),
            UnitsInStock = 50 + (i % 200),
            Discontinued = i % 10 == 0
        }).ToList();

        _repoDbProducts = Enumerable.Range(0, BatchSize).Select(i => new RepoDbProduct
        {
            ProductName = $"Bulk Product {i}",
            UnitPrice = 10.00m + (i % 100),
            UnitsInStock = 50 + (i % 200),
            Discontinued = i % 10 == 0 ? 1 : 0
        }).ToList();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM benchmark_products";
        cmd.ExecuteNonQuery();
    }

    // --- Jaunty BulkInsert ---

    [Benchmark(Description = "Jaunty BulkInsert")]
    public void Jaunty_BulkInsert()
    {
        _connection.BulkInsert(_jauntyProducts);
    }

    // --- Dapper loop insert (no built-in bulk) ---

    [Benchmark(Description = "Dapper Execute loop", Baseline = true)]
    public void Dapper_LoopInsert()
    {
        foreach (var p in _jauntyProducts)
        {
            _connection.Execute(
                "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@ProductName, @UnitPrice, @UnitsInStock, @Discontinued)",
                new { p.ProductName, p.UnitPrice, p.UnitsInStock, p.Discontinued });
        }
    }

    // --- RepoDb InsertAll ---

    [Benchmark(Description = "RepoDb InsertAll")]
    public void RepoDb_InsertAll()
    {
        RepoDb.DbConnectionExtension.InsertAll(_connection, _repoDbProducts);
    }
}
