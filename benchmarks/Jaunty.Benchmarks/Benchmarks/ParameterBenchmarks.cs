using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;

namespace Jaunty.Benchmarks.Benchmarks;

/// <summary>
/// Jaunty-specific benchmark comparing parameter binding strategies.
/// </summary>
public class ParameterBenchmarks
{
    private DbConnection _connection = null!;

    [Params(DatabaseProvider.Sqlite, DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MariaDb)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

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

    // --- Named parameter (anonymous object) ---

    [Benchmark(Description = "Jaunty named param (anon obj)", Baseline = true)]
    public JauntyProduct Jaunty_NamedParameter()
    {
        return _connection.QueryFirst<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = @Id",
            new { Id = 1 });
    }

    // --- Positional parameter ---

    [Benchmark(Description = "Jaunty positional param")]
    public JauntyProduct Jaunty_PositionalParameter()
    {
        return _connection.QueryFirst<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = @p0",
            1);
    }

    // --- Dapper named parameter for reference ---

    [Benchmark(Description = "Dapper named param (anon obj)")]
    public DapperProduct Dapper_NamedParameter()
    {
        return _connection.QueryFirst<DapperProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products WHERE product_id = @Id",
            new { Id = 1 });
    }
}
