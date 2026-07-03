using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;

using Microsoft.Data.SqlClient;
#if NET8_0_OR_GREATER
using Microsoft.Data.Sqlite;
#endif

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Helpers.Dialects;

public sealed class DialectFixture : IDisposable
{
    private static readonly object SqlServerCompatLock = new();
    private static bool _sqlServerCompatInitialized;

    public IDbConnection GetClosedConnection(DialectInfo dialect)
    {
        return CreateConnection(dialect);
    }

    public IDbConnection GetConnection(DialectInfo dialect)
    {
        var connection = CreateConnection(dialect);
        if (connection.State == ConnectionState.Closed)
        {
            connection.Open();
            EnsureDialectCompatibility(connection, dialect);
        }

        return connection;
    }

    public DbConnection GetClosedDbConnection(DialectInfo dialect)
    {
        return CreateConnection(dialect);
    }

    public DbConnection GetDbConnection(DialectInfo dialect)
    {
        var connection = CreateConnection(dialect);
        if (connection.State == ConnectionState.Closed)
        {
            connection.Open();
            EnsureDialectCompatibility(connection, dialect);
        }

        return connection;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static DbConnection CreateConnection(DialectInfo dialect)
    {
        return dialect.Provider switch
        {
            DialectProvider.SystemSqlite => new SQLiteConnection($"Data Source={ResolveNorthwindPath()}"),
#if NET8_0_OR_GREATER
            DialectProvider.MicrosoftSqlite => new SqliteConnection($"Data Source={ResolveNorthwindPath()}"),
#endif
            DialectProvider.SqlServer => new SqlConnection(TestConfiguration.SqlServerConnectionString),
            DialectProvider.Postgres => new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString),
            DialectProvider.MariaDb => new MySqlConnection(TestConfiguration.MariaDbConnectionString),
            _ => throw new InvalidOperationException($"Unsupported dialect provider: {dialect.Provider}")
        };
    }

    private static void EnsureDialectCompatibility(DbConnection connection, DialectInfo dialect)
    {
        if (dialect.Provider == DialectProvider.SqlServer)
            EnsureSqlServerCompatibility(connection);
    }

    private static void EnsureSqlServerCompatibility(DbConnection connection)
    {
        if (_sqlServerCompatInitialized) return;

        lock (SqlServerCompatLock)
        {
            if (_sqlServerCompatInitialized) return;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Categories', 'category_id') IS NULL ALTER TABLE dbo.Categories ADD category_id AS (CategoryId);
    IF COL_LENGTH('dbo.Categories', 'category_name') IS NULL ALTER TABLE dbo.Categories ADD category_name AS (CategoryName);
END;

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Products', 'product_id') IS NULL ALTER TABLE dbo.Products ADD product_id AS (ProductId);
    IF COL_LENGTH('dbo.Products', 'product_name') IS NULL ALTER TABLE dbo.Products ADD product_name AS (ProductName);
    IF COL_LENGTH('dbo.Products', 'supplier_id') IS NULL ALTER TABLE dbo.Products ADD supplier_id AS (SupplierId);
    IF COL_LENGTH('dbo.Products', 'category_id') IS NULL ALTER TABLE dbo.Products ADD category_id AS (CategoryId);
    IF COL_LENGTH('dbo.Products', 'quantity_per_unit') IS NULL ALTER TABLE dbo.Products ADD quantity_per_unit AS (QuantityPerUnit);
    IF COL_LENGTH('dbo.Products', 'unit_price') IS NULL ALTER TABLE dbo.Products ADD unit_price AS (UnitPrice);
    IF COL_LENGTH('dbo.Products', 'units_in_stock') IS NULL ALTER TABLE dbo.Products ADD units_in_stock AS (UnitsInStock);
    IF COL_LENGTH('dbo.Products', 'units_on_order') IS NULL ALTER TABLE dbo.Products ADD units_on_order AS (UnitsOnOrder);
    IF COL_LENGTH('dbo.Products', 'reorder_level') IS NULL ALTER TABLE dbo.Products ADD reorder_level AS (ReorderLevel);
    IF COL_LENGTH('dbo.Products', 'discontinued') IS NULL ALTER TABLE dbo.Products ADD discontinued AS (Discontinued);
END;

IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Customers', 'customer_id') IS NULL ALTER TABLE dbo.Customers ADD customer_id AS (CustomerId);
    IF COL_LENGTH('dbo.Customers', 'company_name') IS NULL ALTER TABLE dbo.Customers ADD company_name AS (CompanyName);
    IF COL_LENGTH('dbo.Customers', 'contact_name') IS NULL ALTER TABLE dbo.Customers ADD contact_name AS (ContactName);
    IF COL_LENGTH('dbo.Customers', 'contact_title') IS NULL ALTER TABLE dbo.Customers ADD contact_title AS (ContactTitle);
    IF COL_LENGTH('dbo.Customers', 'address') IS NULL ALTER TABLE dbo.Customers ADD address AS (Address);
    IF COL_LENGTH('dbo.Customers', 'city') IS NULL ALTER TABLE dbo.Customers ADD city AS (City);
    IF COL_LENGTH('dbo.Customers', 'region') IS NULL ALTER TABLE dbo.Customers ADD region AS (Region);
    IF COL_LENGTH('dbo.Customers', 'postal_code') IS NULL ALTER TABLE dbo.Customers ADD postal_code AS (PostalCode);
    IF COL_LENGTH('dbo.Customers', 'country') IS NULL ALTER TABLE dbo.Customers ADD country AS (Country);
    IF COL_LENGTH('dbo.Customers', 'phone') IS NULL ALTER TABLE dbo.Customers ADD phone AS (Phone);
    IF COL_LENGTH('dbo.Customers', 'fax') IS NULL ALTER TABLE dbo.Customers ADD fax AS (Fax);
END;

IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Orders', 'order_id') IS NULL ALTER TABLE dbo.Orders ADD order_id AS (OrderId);
    IF COL_LENGTH('dbo.Orders', 'customer_id') IS NULL ALTER TABLE dbo.Orders ADD customer_id AS (CustomerId);
    IF COL_LENGTH('dbo.Orders', 'employee_id') IS NULL ALTER TABLE dbo.Orders ADD employee_id AS (EmployeeId);
    IF COL_LENGTH('dbo.Orders', 'order_date') IS NULL ALTER TABLE dbo.Orders ADD order_date AS (OrderDate);
    IF COL_LENGTH('dbo.Orders', 'required_date') IS NULL ALTER TABLE dbo.Orders ADD required_date AS (RequiredDate);
    IF COL_LENGTH('dbo.Orders', 'shipped_date') IS NULL ALTER TABLE dbo.Orders ADD shipped_date AS (ShippedDate);
    IF COL_LENGTH('dbo.Orders', 'ship_via') IS NULL ALTER TABLE dbo.Orders ADD ship_via AS (ShipVia);
    IF COL_LENGTH('dbo.Orders', 'freight') IS NULL ALTER TABLE dbo.Orders ADD freight AS (Freight);
    IF COL_LENGTH('dbo.Orders', 'ship_name') IS NULL ALTER TABLE dbo.Orders ADD ship_name AS (ShipName);
    IF COL_LENGTH('dbo.Orders', 'ship_address') IS NULL ALTER TABLE dbo.Orders ADD ship_address AS (ShipAddress);
    IF COL_LENGTH('dbo.Orders', 'ship_city') IS NULL ALTER TABLE dbo.Orders ADD ship_city AS (ShipCity);
    IF COL_LENGTH('dbo.Orders', 'ship_region') IS NULL ALTER TABLE dbo.Orders ADD ship_region AS (ShipRegion);
    IF COL_LENGTH('dbo.Orders', 'ship_postal_code') IS NULL ALTER TABLE dbo.Orders ADD ship_postal_code AS (ShipPostalCode);
    IF COL_LENGTH('dbo.Orders', 'ship_country') IS NULL ALTER TABLE dbo.Orders ADD ship_country AS (ShipCountry);
END;

IF OBJECT_ID('dbo.Suppliers', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Suppliers', 'supplier_id') IS NULL ALTER TABLE dbo.Suppliers ADD supplier_id AS (SupplierId);
    IF COL_LENGTH('dbo.Suppliers', 'company_name') IS NULL ALTER TABLE dbo.Suppliers ADD company_name AS (CompanyName);
    IF COL_LENGTH('dbo.Suppliers', 'contact_name') IS NULL ALTER TABLE dbo.Suppliers ADD contact_name AS (ContactName);
    IF COL_LENGTH('dbo.Suppliers', 'contact_title') IS NULL ALTER TABLE dbo.Suppliers ADD contact_title AS (ContactTitle);
    IF COL_LENGTH('dbo.Suppliers', 'address') IS NULL ALTER TABLE dbo.Suppliers ADD address AS (Address);
    IF COL_LENGTH('dbo.Suppliers', 'city') IS NULL ALTER TABLE dbo.Suppliers ADD city AS (City);
    IF COL_LENGTH('dbo.Suppliers', 'region') IS NULL ALTER TABLE dbo.Suppliers ADD region AS (Region);
    IF COL_LENGTH('dbo.Suppliers', 'postal_code') IS NULL ALTER TABLE dbo.Suppliers ADD postal_code AS (PostalCode);
    IF COL_LENGTH('dbo.Suppliers', 'country') IS NULL ALTER TABLE dbo.Suppliers ADD country AS (Country);
    IF COL_LENGTH('dbo.Suppliers', 'phone') IS NULL ALTER TABLE dbo.Suppliers ADD phone AS (Phone);
    IF COL_LENGTH('dbo.Suppliers', 'fax') IS NULL ALTER TABLE dbo.Suppliers ADD fax AS (Fax);
END;
";
            cmd.ExecuteNonQuery();
            _sqlServerCompatInitialized = true;
        }
    }

    private static string ResolveNorthwindPath()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "data", "sqlite", "Northwind.db");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new FileNotFoundException("Could not locate data/sqlite/Northwind.db from test output directory.");
    }

    #region Write Context Support

    public WriteDialectContext GetWriteContext(DialectInfo dialect)
    {
        return dialect.Provider switch
        {
            DialectProvider.SystemSqlite => CreateSystemSqliteContext(),
#if NET8_0_OR_GREATER
            DialectProvider.MicrosoftSqlite => CreateMicrosoftSqliteContext(),
#else
            DialectProvider.MicrosoftSqlite => throw new NotSupportedException("Microsoft.Data.Sqlite is not available on .NET Framework."),
#endif
            DialectProvider.SqlServer => CreateServerContext(new SqlConnection(TestConfiguration.SqlServerConnectionString), DialectProvider.SqlServer),
            DialectProvider.Postgres => CreateServerContext(new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString), DialectProvider.Postgres),
            DialectProvider.MariaDb => CreateServerContext(new MySqlConnection(TestConfiguration.MariaDbConnectionString), DialectProvider.MariaDb),
            _ => throw new InvalidOperationException($"Unsupported dialect provider: {dialect.Provider}")
        };
    }

    private static WriteDialectContext CreateSystemSqliteContext()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        InitializeBulkSchema(connection, DialectProvider.SystemSqlite);
        return new WriteDialectContext(connection, transaction: null);
    }

#if NET8_0_OR_GREATER
    private static WriteDialectContext CreateMicrosoftSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InitializeBulkSchema(connection, DialectProvider.MicrosoftSqlite);
        return new WriteDialectContext(connection, transaction: null);
    }
#endif

    private static WriteDialectContext CreateServerContext(DbConnection connection, DialectProvider provider)
    {
        connection.Open();
        InitializeBulkSchema(connection, provider);
        return new WriteDialectContext(connection, transaction: null);
    }

    private static void InitializeBulkSchema(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer => @"
            IF OBJECT_ID('dbo.bulk_test', 'U') IS NOT NULL DROP TABLE dbo.bulk_test;
            CREATE TABLE dbo.bulk_test (
                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                name NVARCHAR(255) NOT NULL,
                value INT NOT NULL
            );",
            DialectProvider.Postgres => @"
            DROP TABLE IF EXISTS bulk_test;
            CREATE TABLE bulk_test (
                id BIGSERIAL PRIMARY KEY,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            );",
            DialectProvider.MariaDb => @"
            DROP TABLE IF EXISTS bulk_test;
            CREATE TABLE bulk_test (
                id BIGINT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                value INT NOT NULL
            );",
            _ => @"
            DROP TABLE IF EXISTS bulk_test;
            CREATE TABLE bulk_test (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            );"
        };
        cmd.ExecuteNonQuery();
    }


    public WriteDialectContext GetWriteContextForTable(DialectInfo dialect, string tableName)
    {
        return dialect.Provider switch
        {
            DialectProvider.SystemSqlite => CreateSqliteContextForTable(new SQLiteConnection("Data Source=:memory:"), tableName, DialectProvider.SystemSqlite),
#if NET8_0_OR_GREATER
            DialectProvider.MicrosoftSqlite => CreateSqliteContextForTable(new SqliteConnection("Data Source=:memory:"), tableName, DialectProvider.MicrosoftSqlite),
#else
            DialectProvider.MicrosoftSqlite => throw new NotSupportedException("Microsoft.Data.Sqlite is not available on .NET Framework."),
#endif
            DialectProvider.SqlServer => CreateServerContextForTable(new SqlConnection(TestConfiguration.SqlServerConnectionString), DialectProvider.SqlServer, tableName),
            DialectProvider.Postgres => CreateServerContextForTable(new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString), DialectProvider.Postgres, tableName),
            DialectProvider.MariaDb => CreateServerContextForTable(new MySqlConnection(TestConfiguration.MariaDbConnectionString), DialectProvider.MariaDb, tableName),
            _ => throw new InvalidOperationException($"Unsupported dialect provider: {dialect.Provider}")
        };
    }

    private static WriteDialectContext CreateSqliteContextForTable(DbConnection connection, string tableName, DialectProvider provider)
    {
        connection.Open();
        InitializeTableSchema(connection, provider, tableName);
        return new WriteDialectContext(connection, transaction: null);
    }

    private static WriteDialectContext CreateServerContextForTable(DbConnection connection, DialectProvider provider, string tableName)
    {
        connection.Open();
        InitializeTableSchema(connection, provider, tableName);
        return new WriteDialectContext(connection, transaction: null);
    }

    private static void InitializeTableSchema(IDbConnection connection, DialectProvider provider, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer =>
                $"IF OBJECT_ID('dbo.{tableName}', 'U') IS NOT NULL DROP TABLE dbo.{tableName}; " +
                $"CREATE TABLE dbo.{tableName} (id BIGINT IDENTITY(1,1) PRIMARY KEY, name NVARCHAR(255) NOT NULL, value INT NOT NULL);",
            DialectProvider.Postgres =>
                $"DROP TABLE IF EXISTS {tableName}; " +
                $"CREATE TABLE {tableName} (id BIGSERIAL PRIMARY KEY, name TEXT NOT NULL, value INTEGER NOT NULL);",
            DialectProvider.MariaDb =>
                $"DROP TABLE IF EXISTS {tableName}; " +
                $"CREATE TABLE {tableName} (id BIGINT AUTO_INCREMENT PRIMARY KEY, name VARCHAR(255) NOT NULL, value INT NOT NULL);",
            _ =>
                $"DROP TABLE IF EXISTS {tableName}; " +
                $"CREATE TABLE {tableName} (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, value INTEGER NOT NULL);"
        };
        cmd.ExecuteNonQuery();
    }

    #endregion
}

public sealed class WriteDialectContext : IDisposable
{
    private readonly IDbTransaction? _transaction;
    private bool _disposed;

    public IDbConnection Connection { get; }

    public CommandOptions CommandOptions { get; }

    public WriteDialectContext(IDbConnection connection, IDbTransaction? transaction)
    {
        Connection = connection;
        _transaction = transaction;
        CommandOptions = transaction is null ? default : CommandOptions.WithTransaction(transaction);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _transaction?.Rollback();
        }
        catch
        {
            // Ignore rollback failures during cleanup.
        }

        _transaction?.Dispose();
        Connection.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
