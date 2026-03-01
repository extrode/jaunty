using System.Data;
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
    private string _adoNetInsertWithIdentitySql = null!;
    private string _dapperInsertWithIdentitySql = null!;

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

        _adoNetInsertWithIdentitySql = Provider switch
        {
            DatabaseProvider.Sqlite => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc); SELECT last_insert_rowid();",
            DatabaseProvider.SqlServer => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
            DatabaseProvider.PostgreSql => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc) RETURNING product_id;",
            DatabaseProvider.MariaDb => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc); SELECT LAST_INSERT_ID();",
            _ => throw new InvalidOperationException()
        };

        _dapperInsertWithIdentitySql = Provider switch
        {
            DatabaseProvider.Sqlite => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@product_name, @unit_price, @units_in_stock, @discontinued); SELECT last_insert_rowid();",
            DatabaseProvider.SqlServer => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@product_name, @unit_price, @units_in_stock, @discontinued); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
            DatabaseProvider.PostgreSql => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@product_name, @unit_price, @units_in_stock, @discontinued) RETURNING product_id;",
            DatabaseProvider.MariaDb => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@product_name, @unit_price, @units_in_stock, @discontinued); SELECT LAST_INSERT_ID();",
            _ => throw new InvalidOperationException()
        };
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

    // --- ADO.NET (hand-coded baseline) ---

    [Benchmark(Description = "ADO.NET ExecuteScalar (INSERT)", Baseline = true)]
    public long AdoNet_Insert()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = _adoNetInsertWithIdentitySql;

        var pName = cmd.CreateParameter();
        pName.ParameterName = "@name";
        pName.Value = "Test Product";
        cmd.Parameters.Add(pName);

        var pPrice = cmd.CreateParameter();
        pPrice.ParameterName = "@price";
        pPrice.Value = 19.99m;
        cmd.Parameters.Add(pPrice);

        var pStock = cmd.CreateParameter();
        pStock.ParameterName = "@stock";
        pStock.Value = 100;
        cmd.Parameters.Add(pStock);

        var pDisc = cmd.CreateParameter();
        pDisc.ParameterName = "@disc";
        pDisc.Value = Provider == DatabaseProvider.Sqlite ? (object)0 : (object)false;
        cmd.Parameters.Add(pDisc);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    // --- Jaunty single insert ---

    [Benchmark(Description = "Jaunty Insert")]
    public long Jaunty_Insert()
    {
        var product = new JauntyProduct
        {
            ProductName = "Test Product",
            UnitPrice = 19.99m,
            UnitsInStock = 100,
            Discontinued = false
        };
        return _connection.Insert(product);
    }

    // --- Dapper insert + identity retrieval (fair comparison) ---

    [Benchmark(Description = "Dapper ExecuteScalar (INSERT)")]
    public long Dapper_InsertWithIdentity()
    {
        return SqlMapper.ExecuteScalar<long>(_connection,
            _dapperInsertWithIdentitySql,
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
