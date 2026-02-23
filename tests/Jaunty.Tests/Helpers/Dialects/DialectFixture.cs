using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Helpers.Dialects;

public sealed class DialectFixture : IDisposable
{
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
            DialectProvider.MicrosoftSqlite => new SqliteConnection($"Data Source={ResolveNorthwindPath()}"),
            DialectProvider.SqlServer => new SqlConnection(TestConfiguration.SqlServerConnectionString),
            DialectProvider.Postgres => new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString),
            DialectProvider.MariaDb => new MySqlConnection(TestConfiguration.MariaDbConnectionString),
            _ => throw new InvalidOperationException($"Unsupported dialect provider: {dialect.Provider}")
        };
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
}
