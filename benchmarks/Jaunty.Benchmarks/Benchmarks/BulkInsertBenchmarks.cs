using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using LinqToDB;
using LinqToDB.Data;

using Microsoft.EntityFrameworkCore;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class BulkInsertBenchmarks
{
    private DbConnection _connection = null!;
    private List<JauntyProduct> _jauntyProducts = null!;
    private List<RepoDbProduct> _repoDbProducts = null!;
    private List<EfProduct> _efProducts = null!;
    private List<Linq2DbProduct> _linq2DbProducts = null!;

    [Params(100, 1_000, 10_000)]
    public int BatchSize { get; set; }

    [Params(DatabaseProvider.Sqlite, DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MariaDb)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

        DatabaseSetup.InitializeRepoDb(Provider);

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
            Discontinued = i % 10 == 0
        }).ToList();

        _efProducts = Enumerable.Range(0, BatchSize).Select(i => new EfProduct
        {
            product_name = $"Bulk Product {i}",
            unit_price = 10.00m + (i % 100),
            units_in_stock = 50 + (i % 200),
            discontinued = i % 10 == 0
        }).ToList();

        _linq2DbProducts = Enumerable.Range(0, BatchSize).Select(i => new Linq2DbProduct
        {
            ProductName = $"Bulk Product {i}",
            UnitPrice = 10.00m + (i % 100),
            UnitsInStock = 50 + (i % 200),
            Discontinued = i % 10 == 0
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

    // --- EF Core AddRange+SaveChanges ---

    [Benchmark(Description = "EF Core AddRange+Save")]
    public void EfCore_BulkInsert()
    {
        using var context = new BenchmarkDbContext(_connection, DatabaseSetup.GetEfProviderName(Provider));
        context.BenchmarkProducts.AddRange(_efProducts);
        context.SaveChanges();
    }

    // --- RepoDb InsertAll ---

    [Benchmark(Description = "RepoDb InsertAll")]
    public void RepoDb_InsertAll()
    {
        RepoDb.DbConnectionExtension.InsertAll(_connection, _repoDbProducts);
    }

    // --- linq2db BulkCopy ---

    [Benchmark(Description = "linq2db BulkCopy")]
    public void Linq2Db_BulkInsert()
    {
        using var db = new BenchmarkDb(_connection, Provider);
        db.BulkCopy(_linq2DbProducts);
    }
}
