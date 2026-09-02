using System.Data.Common;

using BenchmarkDotNet.Attributes;

using Dapper;

using Jaunty.Benchmarks.Config;
using Jaunty.Benchmarks.Entities;
using Jaunty.Core;

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

        var doubleMapper = Provider == DatabaseProvider.Sqlite ? SqliteDoubleMapper.Mapper : CustomMapper.Mapper;
        _doubleMapper = new CommandOptions<JauntyProduct>(mapper: doubleMapper);
        _doubleMapperWithHint = new CommandOptions<JauntyProduct>(mapper: doubleMapper, expectedRowCount: RowCount);
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
        var results = new List<JauntyProduct>(RowCount);
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

    [Benchmark(Description = "Jaunty Query<T> (WithExpectedRowCount)")]
    public List<JauntyProduct> Jaunty_QueryWithExpectedRowCount()
    {
        var options = CommandOptions<JauntyProduct>.WithExpectedRowCount(RowCount);
        return _connection.Query(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: options);
    }

    // Same getters, same ordinals as the hand-coded baseline, so the difference between the two
    // is Jaunty's pipeline overhead with mapping taken out of the comparison.
    private static readonly CommandOptions<JauntyProduct> CustomMapper =
        CommandOptions<JauntyProduct>.WithMapper(static reader => new JauntyProduct
        {
            ProductId = reader.GetInt32(0),
            ProductName = reader.GetString(1),
            UnitPrice = reader.GetDecimal(2),
            UnitsInStock = reader.GetInt32(3),
            Discontinued = reader.GetBoolean(4)
        });

    [Benchmark(Description = "Jaunty Query<T> (custom mapper)")]
    public List<JauntyProduct> Jaunty_QueryWithCustomMapper()
    {
        return _connection.Query(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: CustomMapper);
    }

    // The baseline sizes its list up front; this gives the custom mapper the same hint, so the two
    // differ only in what Jaunty adds around the loop.
    [Benchmark(Description = "Jaunty Query<T> (custom mapper, WithExpectedRowCount)")]
    public List<JauntyProduct> Jaunty_QueryWithCustomMapperAndExpectedRowCount()
    {
        var options = new CommandOptions<JauntyProduct>(mapper: CustomMapper.Mapper, expectedRowCount: RowCount);
        return _connection.Query(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: options);
    }

    // The lesson the generated mapper learned, applied by hand. Microsoft.Data.Sqlite implements
    // GetDecimal on a REAL column as text formatting plus decimal.Parse, so on SQLite the price is
    // read as the double it is stored as and cast (measured 2026-09-02: 4.8 ms vs 1.8 ms per 10k
    // rows). The other providers store a real decimal and GetDouble would throw, so there Setup
    // falls back to CustomMapper and the two cases read the same; only the SQLite column compares.
    private static readonly CommandOptions<JauntyProduct> SqliteDoubleMapper =
        CommandOptions<JauntyProduct>.WithMapper(static reader => new JauntyProduct
        {
            ProductId = reader.GetInt32(0),
            ProductName = reader.GetString(1),
            UnitPrice = (decimal)reader.GetDouble(2),
            UnitsInStock = reader.GetInt32(3),
            Discontinued = reader.GetBoolean(4)
        });

    private CommandOptions<JauntyProduct> _doubleMapper;
    private CommandOptions<JauntyProduct> _doubleMapperWithHint;

    [Benchmark(Description = "Jaunty Query<T> (custom mapper, GetDouble)")]
    public List<JauntyProduct> Jaunty_QueryWithDoubleMapper()
    {
        return _connection.Query(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: _doubleMapper);
    }

    [Benchmark(Description = "Jaunty Query<T> (custom mapper, GetDouble, WithExpectedRowCount)")]
    public List<JauntyProduct> Jaunty_QueryWithDoubleMapperAndExpectedRowCount()
    {
        return _connection.Query(
            "SELECT product_id, product_name, unit_price, units_in_stock, discontinued FROM benchmark_products",
            options: _doubleMapperWithHint);
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