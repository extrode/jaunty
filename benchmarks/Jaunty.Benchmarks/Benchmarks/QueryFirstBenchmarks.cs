using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using LinqToDB;

using Microsoft.EntityFrameworkCore;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryFirstBenchmarks
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

    // --- EF Core ---

    [Benchmark(Description = "EF Core First")]
    public EfProduct EfCore_QueryFirst()
    {
        using var context = new BenchmarkDbContext(_connection, DatabaseSetup.GetEfProviderName(Provider));
        return context.BenchmarkProducts
            .FromSqlRaw("SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = {0}", 1)
            .First();
    }

    // --- RepoDb ---

    [Benchmark(Description = "RepoDb Query (first)")]
    public RepoDbProduct RepoDb_QueryFirst()
    {
        return RepoDb.DbConnectionExtension.Query<RepoDbProduct>(_connection, 1).First();
    }

    // --- linq2db ---

    [Benchmark(Description = "linq2db First")]
    public Linq2DbProduct Linq2Db_QueryFirst()
    {
        using var db = new BenchmarkDb(_connection, Provider);
        return db.BenchmarkProducts.First(p => p.ProductId == 1);
    }
}
