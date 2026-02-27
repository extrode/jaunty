using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using LinqToDB;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class InsertBenchmarks
{
    private DbConnection _connection = null!;

    [Params(DatabaseProvider.Sqlite, DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MariaDb)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

        DatabaseSetup.InitializeRepoDb(Provider);
        DatabaseSetup.EnsureDatabaseExists(Provider);

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

    // --- EF Core single insert ---

    [Benchmark(Description = "EF Core Add+Save")]
    public void EfCore_Insert()
    {
        using var context = new BenchmarkDbContext(_connection, DatabaseSetup.GetEfProviderName(Provider));
        context.BenchmarkProducts.Add(new EfProduct
        {
            product_name = "Test Product",
            unit_price = 19.99m,
            units_in_stock = 100,
            discontinued = false
        });
        context.SaveChanges();
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
            Discontinued = false
        };
        RepoDb.DbConnectionExtension.Insert(_connection, product);
    }

    // --- linq2db single insert ---

    [Benchmark(Description = "linq2db Insert")]
    public void Linq2Db_Insert()
    {
        using var db = new BenchmarkDb(_connection, Provider);
        db.Insert(new Linq2DbProduct
        {
            ProductName = "Test Product",
            UnitPrice = 19.99m,
            UnitsInStock = 100,
            Discontinued = false
        });
    }
}
