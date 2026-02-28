using System.Data;
using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

using MySqlConnector;

using Npgsql;

using RepoDb;
using RepoDb.Interfaces;
using RepoDb.Options;

namespace Jaunty.Benchmarks.Config;

/// <summary>
/// Handles SQLite Int64 → bool conversion for RepoDb.
/// SQLite stores BOOLEAN as INTEGER (0/1); RepoDb can't coerce Int64 to bool natively.
/// </summary>
internal class SqliteInt64BoolHandler : IPropertyHandler<long?, bool?>
{
    public bool? Get(long? input, PropertyHandlerGetOptions options) => input.HasValue ? input.Value != 0 : null;
    public long? Set(bool? input, PropertyHandlerSetOptions options) => input.HasValue ? (input.Value ? 1L : 0L) : null;
}

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

    private static bool _repoDbInitialized;

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

    /// <summary>
    /// Ensures the benchmark database exists for non-SQLite providers.
    /// </summary>
    public static void EnsureDatabaseExists(DatabaseProvider provider)
    {
        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                return; // In-memory, always exists

            case DatabaseProvider.SqlServer:
            {
                var builder = new SqlConnectionStringBuilder(SqlServerConnectionString);
                var dbName = builder.InitialCatalog;
                builder.InitialCatalog = "master";
                using var conn = new SqlConnection(builder.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{dbName}') CREATE DATABASE [{dbName}]";
                cmd.ExecuteNonQuery();
                break;
            }

            case DatabaseProvider.PostgreSql:
            {
                var builder = new NpgsqlConnectionStringBuilder(PostgreSqlConnectionString);
                var dbName = builder.Database;
                builder.Database = "postgres";
                using var conn = new NpgsqlConnection(builder.ConnectionString);
                conn.Open();
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'";
                if (checkCmd.ExecuteScalar() == null)
                {
                    using var createCmd = conn.CreateCommand();
                    createCmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
                    createCmd.ExecuteNonQuery();
                }
                break;
            }

            case DatabaseProvider.MariaDb:
            {
                var builder = new MySqlConnectionStringBuilder(MariaDbConnectionString);
                var dbName = builder.Database;
                builder.Database = "";
                using var conn = new MySqlConnection(builder.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}`";
                cmd.ExecuteNonQuery();
                break;
            }
        }
    }

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
        pName.DbType = DbType.String;
        pName.Size = 200;
        cmd.Parameters.Add(pName);

        var pPrice = cmd.CreateParameter();
        pPrice.ParameterName = "@price";
        pPrice.DbType = DbType.Decimal;
        cmd.Parameters.Add(pPrice);

        var pStock = cmd.CreateParameter();
        pStock.ParameterName = "@stock";
        pStock.DbType = DbType.Int32;
        cmd.Parameters.Add(pStock);

        var pDisc = cmd.CreateParameter();
        pDisc.ParameterName = "@disc";
        pDisc.DbType = provider == DatabaseProvider.Sqlite ? DbType.Int64 : DbType.Boolean;
        cmd.Parameters.Add(pDisc);

        // Prepare() optimizes repeated execution but SqlCommand.Prepare requires SqlDbType
        // (not generic DbType) to be explicitly set, which isn't possible through DbParameter.
        // Skip Prepare() for SQL Server; the perf difference is negligible for seeding.
        if (provider != DatabaseProvider.SqlServer)
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
    /// Initializes RepoDb for all providers. Must be called once during GlobalSetup.
    /// Registers all providers at once since BDN may run multiple providers in the same process.
    /// </summary>
    public static void InitializeRepoDb(DatabaseProvider provider)
    {
        if (_repoDbInitialized) return;
        _repoDbInitialized = true;

        GlobalConfiguration.Setup()
            .UseSqlite()
            .UseSqlServer()
            .UsePostgreSql()
            .UseMySqlConnector();

        TypeMapper.Add(typeof(bool), DbType.Int64);

        // RepoDb can't coerce SQLite's Int64 to bool natively; register a property handler.
        PropertyHandlerMapper.Add(typeof(bool), new SqliteInt64BoolHandler(), true);
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
