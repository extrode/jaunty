using System.Data;
using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

using Npgsql;

namespace Jaunty.Benchmarks.Config;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
    PostgreSql
}

public static class DatabaseSetup
{
    private static readonly string? SqlServerConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER");

    private static readonly string? PostgreSqlConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL");

    public static bool IsAvailable(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => true,
        DatabaseProvider.SqlServer => !string.IsNullOrWhiteSpace(SqlServerConnectionString),
        DatabaseProvider.PostgreSql => !string.IsNullOrWhiteSpace(PostgreSqlConnectionString),
        _ => false
    };

    public static DbConnection CreateConnection(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => new SqliteConnection("Data Source=:memory:"),
        DatabaseProvider.SqlServer => new SqlConnection(SqlServerConnectionString),
        DatabaseProvider.PostgreSql => new NpgsqlConnection(PostgreSqlConnectionString),
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

        // Reset identity for SQLite
        if (provider == DatabaseProvider.Sqlite)
        {
            using var reset = connection.CreateCommand();
            reset.CommandText = "DELETE FROM sqlite_sequence WHERE name='benchmark_products'";
            try { reset.ExecuteNonQuery(); } catch { /* table may not exist */ }
        }

        // Bulk seed
        for (int i = 0; i < rowCount; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = provider switch
            {
                DatabaseProvider.Sqlite => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc)",
                DatabaseProvider.SqlServer => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc)",
                DatabaseProvider.PostgreSql => "INSERT INTO benchmark_products (product_name, unit_price, units_in_stock, discontinued) VALUES (@name, @price, @stock, @disc)",
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };

            AddParameter(cmd, "@name", $"Product {i + 1}");
            AddParameter(cmd, "@price", 10.00m + (i % 100));
            AddParameter(cmd, "@stock", 50 + (i % 200));
            AddParameter(cmd, "@disc", i % 10 == 0);
            cmd.ExecuteNonQuery();
        }
    }

    private static void AddParameter(IDbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
