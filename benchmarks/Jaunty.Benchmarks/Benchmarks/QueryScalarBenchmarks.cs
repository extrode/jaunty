using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Jaunty.Benchmarks.Config;

namespace Jaunty.Benchmarks.Benchmarks;

public class QueryScalarBenchmarks
{
    private DbConnection _connection = null!;

    [Params(DatabaseProvider.Sqlite)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

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

    // --- Jaunty ---

    [Benchmark(Description = "Jaunty QueryScalar")]
    public int Jaunty_QueryScalar()
    {
        return _connection.QueryScalar<int>("SELECT COUNT(*) FROM benchmark_products");
    }

    // --- Dapper ---

    [Benchmark(Description = "Dapper ExecuteScalar", Baseline = true)]
    public int Dapper_QueryScalar()
    {
        return _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM benchmark_products");
    }
}
