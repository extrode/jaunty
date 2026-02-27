using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class InsertBenchmarks
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
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clear table before each iteration
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM benchmark_products";
        cmd.ExecuteNonQuery();
    }

    // --- Jaunty single insert ---

    [Benchmark(Description = "Jaunty Insert")]
    public void Jaunty_Insert()
    {
        var product = new JauntyProduct
        {
            ProductName = "Test Product",
            UnitPrice = 19.99m,
            UnitsInStock = 100,
            Discontinued = false
        };
        _connection.Insert(product);
    }

    // --- Dapper single insert ---

    [Benchmark(Description = "Dapper Execute (INSERT)", Baseline = true)]
    public void Dapper_Insert()
    {
        _connection.Execute(
            "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@product_name, @unit_price, @units_in_stock, @discontinued)",
            new { product_name = "Test Product", unit_price = 19.99m, units_in_stock = 100, discontinued = false });
    }

    // --- RepoDb single insert ---

    [Benchmark(Description = "RepoDb Insert")]
    public void RepoDb_Insert()
    {
        var product = new RepoDbProduct
        {
            ProductName = "Test Product",
            UnitPrice = 19.99m,
            UnitsInStock = 100,
            Discontinued = 0
        };
        RepoDb.DbConnectionExtension.Insert(_connection, product);
    }
}
