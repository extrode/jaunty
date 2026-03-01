using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using Microsoft.EntityFrameworkCore;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryBenchmarks
{
    private DbConnection _connection = null!;

    [Params(1, 100, 10_000)]
    public int RowCount { get; set; }

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
        DatabaseSetup.SeedData(_connection, Provider, RowCount);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // --- ADO.NET (hand-coded baseline) ---

    [Benchmark(Description = "ADO.NET (hand-coded)", Baseline = true)]
    public List<JauntyProduct> AdoNet_Query()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products";
        using var reader = cmd.ExecuteReader();
        var results = new List<JauntyProduct>();
        while (reader.Read())
        {
            results.Add(new JauntyProduct
            {
                ProductId = reader.GetInt32(0),
                ProductName = reader.GetString(1),
                UnitPrice = reader.GetDecimal(2),
                UnitsInStock = reader.GetInt32(3),
                Discontinued = reader.GetBoolean(4)
            });
        }
        return results;
    }

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty Query<T>")]
    public List<JauntyProduct> Jaunty_Query()
    {
        return _connection.Query<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products");
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper Query<T>")]
    public List<DapperProduct> Dapper_Query()
    {
        return SqlMapper.Query<DapperProduct>(_connection,
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products")
            .AsList();
    }

    // --- EF Core ---

    [Benchmark(Description = "EF Core ToList")]
    public List<EfProduct> EfCore_Query()
    {
        using var context = new BenchmarkDbContext(_connection, DatabaseSetup.GetEfProviderName(Provider));
        return context.BenchmarkProducts
            .FromSqlRaw("SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products")
            .ToList();
    }

    // --- RepoDb ---

    [Benchmark(Description = "RepoDb QueryAll")]
    public List<RepoDbProduct> RepoDb_Query()
    {
        return RepoDb.DbConnectionExtension.QueryAll<RepoDbProduct>(_connection).AsList();
    }

    // --- linq2db ---

    [Benchmark(Description = "linq2db Query")]
    public List<Linq2DbProduct> Linq2Db_Query()
    {
        using var db = new BenchmarkDb(_connection, Provider);
        return db.BenchmarkProducts.ToList();
    }
}
