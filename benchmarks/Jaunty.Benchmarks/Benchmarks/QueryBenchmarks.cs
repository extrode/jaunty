using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using Microsoft.EntityFrameworkCore;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryBenchmarks
{
    private DbConnection _connection = null!;

    [Params(1, 10, 100, 1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [Params(DatabaseProvider.Sqlite)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

        // Initialize RepoDb (with SQLite boolean type mapping)
        RepoDb.GlobalConfiguration.Setup().UseSqlite();
        RepoDb.TypeMapper.Add(typeof(bool), System.Data.DbType.Int64);

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

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty Query<T>")]
    public List<JauntyProduct> Jaunty_Query()
    {
        return _connection.Query<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products");
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper Query<T>", Baseline = true)]
    public List<DapperProduct> Dapper_Query()
    {
        return _connection.Query<DapperProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products")
            .AsList();
    }

    // --- EF Core ---
    // Note: EF Core with in-memory SQLite requires sharing the open connection.
    // We use FromSqlRaw on the shared connection to keep parity.

    [Benchmark(Description = "EF Core ToList")]
    public List<EfProduct> EfCore_Query()
    {
        using var context = CreateEfContext();
        return context.BenchmarkProducts
            .FromSqlRaw("SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products")
            .ToList();
    }

    private BenchmarkDbContext CreateEfContext()
    {
        var providerName = Provider switch
        {
            DatabaseProvider.SqlServer => "sqlserver",
            DatabaseProvider.PostgreSql => "postgresql",
            _ => "sqlite"
        };
        return new BenchmarkDbContext(_connection, providerName);
    }

    // --- RepoDb ---

    [Benchmark(Description = "RepoDb QueryAll")]
    public List<RepoDbProduct> RepoDb_Query()
    {
        return RepoDb.DbConnectionExtension.QueryAll<RepoDbProduct>(_connection).AsList();
    }
}
