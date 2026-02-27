using System.Data;
using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

using MySqlConnector;

using Npgsql;

using RepoDb.DbHelpers;
using RepoDb.DbSettings;
using RepoDb.StatementBuilders;

namespace Jaunty.Benchmarks.Config;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
    PostgreSql,
    MariaDb
}

public static class DatabaseSetup
{
    private static readonly string SqlServerConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER")
        ?? "Server=localhost;Database=JauntyBench;Trusted_Connection=True;TrustServerCertificate=True;";

    private static readonly string PostgreSqlConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL")
        ?? "Host=localhost;Database=jauntybench;Username=postgres;";

    private static readonly string MariaDbConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB")
        ?? "Server=localhost;Database=jauntybench;User=root;";

    public static bool IsAvailable(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => true,
        DatabaseProvider.SqlServer => true,
        DatabaseProvider.PostgreSql => true,
        DatabaseProvider.MariaDb => true,
        _ => false
    };

    public static DbConnection CreateConnection(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => new SqliteConnection("Data Source=:memory:"),
        DatabaseProvider.SqlServer => new SqlConnection(SqlServerConnectionString),
        DatabaseProvider.PostgreSql => new NpgsqlConnection(PostgreSqlConnectionString),
        DatabaseProvider.MariaDb => new MySqlConnection(MariaDbConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static void CreateSchema(DbConnection connection, DatabaseProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DatabaseProvider.Sqlite => """
                CREATE TABLE IF NOT EXISTS benchmark_products (
                    product_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_name TEXT NOT NULL,
                    unit_price REAL NOT NULL,
                    units_in_stock INTEGER NOT NULL,
                    discontinued INTEGER NOT NULL DEFAULT 0
                );
                """,

            DatabaseProvider.SqlServer => """
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'benchmark_products')
                CREATE TABLE benchmark_products (
                    product_id INT IDENTITY(1,1) PRIMARY KEY,
                    product_name NVARCHAR(200) NOT NULL,
                    unit_price DECIMAL(18,2) NOT NULL,
                    units_in_stock INT NOT NULL,
                    discontinued BIT NOT NULL DEFAULT 0
                );
                """,

            DatabaseProvider.PostgreSql => """
                CREATE TABLE IF NOT EXISTS benchmark_products (
                    product_id SERIAL PRIMARY KEY,
                    product_name VARCHAR(200) NOT NULL,
                    unit_price NUMERIC(18,2) NOT NULL,
                    units_in_stock INT NOT NULL,
                    discontinued BOOLEAN NOT NULL DEFAULT FALSE
                );
                """,

            DatabaseProvider.MariaDb => """
                CREATE TABLE IF NOT EXISTS benchmark_products (
                    product_id INT AUTO_INCREMENT PRIMARY KEY,
                    product_name VARCHAR(200) NOT NULL,
                    unit_price DECIMAL(18,2) NOT NULL,
                    units_in_stock INT NOT NULL,
                    discontinued BOOLEAN NOT NULL DEFAULT FALSE
                );
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        cmd.ExecuteNonQuery();
    }

    public static void SeedData(DbConnection connection, DatabaseProvider provider, int rowCount)
    {
        // Clear existing data
        using (var del = connection.CreateCommand())
        {
            del.CommandText = "DELETE FROM benchmark_products";
            del.ExecuteNonQuery();
        }

        // Reset identity
        ResetIdentity(connection, provider);

        // Transaction-wrapped, prepared-statement seeding for efficient bulk insert.
        // Single command + parameter reuse = ~100x faster than per-row command creation.
        using var transaction = connection.BeginTransaction();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc)";

        var pName = cmd.CreateParameter();
        pName.ParameterName = "@name";
        cmd.Parameters.Add(pName);

        var pPrice = cmd.CreateParameter();
        pPrice.ParameterName = "@price";
        cmd.Parameters.Add(pPrice);

        var pStock = cmd.CreateParameter();
        pStock.ParameterName = "@stock";
        cmd.Parameters.Add(pStock);

        var pDisc = cmd.CreateParameter();
        pDisc.ParameterName = "@disc";
        cmd.Parameters.Add(pDisc);

        cmd.Prepare();

        for (int i = 0; i < rowCount; i++)
        {
            pName.Value = $"Product {i + 1}";
            pPrice.Value = 10.00m + (i % 100);
            pStock.Value = 50 + (i % 200);
            // SQLite stores booleans as 0/1 integers; use int for SQLite, bool for others
            pDisc.Value = provider == DatabaseProvider.Sqlite
                ? (object)(i % 10 == 0 ? 1 : 0)
                : (object)(i % 10 == 0);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void ResetIdentity(DbConnection connection, DatabaseProvider provider)
    {
        using var cmd = connection.CreateCommand();

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                cmd.CommandText = "DELETE FROM sqlite_sequence WHERE name='benchmark_products'";
                try { cmd.ExecuteNonQuery(); } catch { /* table may not exist */ }
                break;

            case DatabaseProvider.SqlServer:
                cmd.CommandText = "DBCC CHECKIDENT ('benchmark_products', RESEED, 0)";
                cmd.ExecuteNonQuery();
                break;

            case DatabaseProvider.PostgreSql:
                cmd.CommandText = "ALTER SEQUENCE benchmark_products_product_id_seq RESTART WITH 1";
                cmd.ExecuteNonQuery();
                break;

            case DatabaseProvider.MariaDb:
                cmd.CommandText = "ALTER TABLE benchmark_products AUTO_INCREMENT = 1";
                cmd.ExecuteNonQuery();
                break;
        }
    }

    /// <summary>
    /// Initializes RepoDb for the given provider. Call once during GlobalSetup.
    /// </summary>
    public static void InitializeRepoDb(DatabaseProvider provider)
    {
        // RepoDb 1.1.x uses automatic initialization based on the connection type
        // No explicit bootstrap setup needed - it's done automatically when using the connection
        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                RepoDb.TypeMapper.Add(typeof(bool), DbType.Int64);
                break;
            case DatabaseProvider.SqlServer:
            case DatabaseProvider.PostgreSql:
            case DatabaseProvider.MariaDb:
                // Type mapper configuration for other providers if needed
                break;
        }
    }

    /// <summary>
    /// Returns the EF Core provider name string for use with BenchmarkDbContext.
    /// </summary>
    public static string GetEfProviderName(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => "sqlite",
        DatabaseProvider.SqlServer => "sqlserver",
        DatabaseProvider.PostgreSql => "postgresql",
        DatabaseProvider.MariaDb => "mariadb",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
