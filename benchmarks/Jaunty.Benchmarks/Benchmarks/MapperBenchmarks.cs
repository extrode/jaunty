using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;
using Jaunty.Core;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Benchmarks.Benchmarks;

/// <summary>
/// Jaunty internal comparison: source-generated mapper vs reflection mapper.
/// </summary>
public class MapperBenchmarks
{
    private DbConnection _connection = null!;

    [Params(DatabaseProvider.Sqlite, DatabaseProvider.SqlServer, DatabaseProvider.PostgreSql, DatabaseProvider.MariaDb)]
    public DatabaseProvider Provider { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!DatabaseSetup.IsAvailable(Provider))
            throw new InvalidOperationException($"{Provider} is not available");

        // Enable reflection mapping for the reflection benchmark
        JauntyReflectionExtensions.UseReflectionMapping();
        SpecialTypeMappers.Register();
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

    // --- Source-generated mapper (IMapped<T>) ---

    [Benchmark(Description = "Source-generated mapper", Baseline = true)]
    public List<JauntyProduct> SourceGenerated()
    {
        return _connection.Query<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products");
    }

    // --- Reflection mapper (Dictionary mapping) ---

    [Benchmark(Description = "Reflection mapper (Dictionary)")]
    public List<Dictionary<string, object>> ReflectionDictionary()
    {
        return _connection.Query<Dictionary<string, object>>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products");
    }

    // --- Custom hand-written mapper ---

    [Benchmark(Description = "Hand-written mapper")]
    public List<JauntyProduct> HandWrittenMapper()
    {
        var options = CommandOptions<JauntyProduct>.WithMapper(static reader => new JauntyProduct
        {
            ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
            ProductName = reader.GetString(reader.GetOrdinal("product_name")),
            UnitPrice = reader.GetDecimal(reader.GetOrdinal("unit_price")),
            UnitsInStock = reader.GetInt32(reader.GetOrdinal("units_in_stock")),
            Discontinued = reader.GetBoolean(reader.GetOrdinal("discontinued"))
        });

        return _connection.Query<JauntyProduct>(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: options);
    }
}
