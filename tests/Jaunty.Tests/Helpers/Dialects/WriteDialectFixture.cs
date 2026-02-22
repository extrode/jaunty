using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using Jaunty.Core;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

#if NET8_0_OR_GREATER
using Microsoft.Data.Sqlite;
#endif

namespace Jaunty.Tests.Helpers.Dialects;

public sealed class WriteDialectFixture : IDisposable
{
    public WriteDialectContext GetWriteContext(DialectInfo dialect)
    {
        return dialect.Provider switch
        {
            DialectProvider.SystemSqlite => CreateSystemSqliteContext(),
#if NET8_0_OR_GREATER
            DialectProvider.MicrosoftSqlite => CreateMicrosoftSqliteContext(),
#endif
            DialectProvider.SqlServer => CreateServerContext(new SqlConnection(TestConfiguration.SqlServerConnectionString)),
            DialectProvider.Postgres => CreateServerContext(new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString)),
            DialectProvider.MariaDb => CreateServerContext(new MySqlConnection(TestConfiguration.MariaDbConnectionString)),
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
        InitializeBulkSchema(connection);
        return new WriteDialectContext(connection, transaction: null);
    }

#if NET8_0_OR_GREATER
    private static WriteDialectContext CreateMicrosoftSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InitializeBulkSchema(connection);
        return new WriteDialectContext(connection, transaction: null);
    }
#endif

    private static WriteDialectContext CreateServerContext(DbConnection connection)
    {
        connection.Open();
        var transaction = connection.BeginTransaction();
        return new WriteDialectContext(connection, transaction);
    }

    private static void InitializeBulkSchema(IDbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE bulk_test (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                value INTEGER NOT NULL
            );";
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
