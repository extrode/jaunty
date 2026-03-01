using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

using LinqToDB;

using RepoDb;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryScalarBenchmarks
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
        DatabaseSetup.SeedData(_connection, Provider, 1000);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // --- ADO.NET (hand-coded baseline) ---

    [Benchmark(Description = "ADO.NET ExecuteScalar", Baseline = true)]
    public int AdoNet_QueryScalar()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM benchmark_products";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty QueryScalar")]
    public int Jaunty_QueryScalar()
    {
        return _connection.QueryScalar<int>("SELECT COUNT(*) FROM benchmark_products");
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper ExecuteScalar")]
    public int Dapper_QueryScalar()
    {
        return _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM benchmark_products");
    }

    // --- EF Core ---

    [Benchmark(Description = "EF Core Count")]
    public int EfCore_QueryScalar()
    {
        using var context = new BenchmarkDbContext(_connection, DatabaseSetup.GetEfProviderName(Provider));
        return context.BenchmarkProducts.Count();
    }

    // --- RepoDb ---

    [Benchmark(Description = "RepoDb CountAll")]
    public long RepoDb_QueryScalar()
    {
        return RepoDb.DbConnectionExtension.CountAll<RepoDbProduct>(_connection);
    }

    // --- linq2db ---

    [Benchmark(Description = "linq2db Count")]
    public int Linq2Db_QueryScalar()
    {
        using var db = new BenchmarkDb(_connection, Provider);
        return db.BenchmarkProducts.Count();
    }
}
