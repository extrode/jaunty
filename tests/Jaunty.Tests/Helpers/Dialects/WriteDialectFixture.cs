using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Helpers.Dialects;

public sealed class WriteDialectFixture : IDisposable
{
    public WriteDialectContext GetWriteContext(DialectInfo dialect)
    {
        return dialect.Provider switch
        {
            DialectProvider.SystemSqlite => CreateSystemSqliteContext(),
            DialectProvider.MicrosoftSqlite => CreateMicrosoftSqliteContext(),
            DialectProvider.SqlServer => CreateServerContext(new SqlConnection(TestConfiguration.SqlServerConnectionString), DialectProvider.SqlServer),
            DialectProvider.Postgres => CreateServerContext(new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString), DialectProvider.Postgres),
            DialectProvider.MariaDb => CreateServerContext(new MySqlConnection(TestConfiguration.MariaDbConnectionString), DialectProvider.MariaDb),
            _ => throw new InvalidOperationException($"Unsupported dialect provider: {dialect.Provider}")
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static WriteDialectContext CreateSystemSqliteContext()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        InitializeBulkSchema(connection, DialectProvider.SystemSqlite);
        return new WriteDialectContext(connection, transaction: null);
    }

    private static WriteDialectContext CreateMicrosoftSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InitializeBulkSchema(connection, DialectProvider.MicrosoftSqlite);
        return new WriteDialectContext(connection, transaction: null);
    }

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
            CREATE TABLE bulk_test (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            );"
        };
        cmd.ExecuteNonQuery();
    }
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
