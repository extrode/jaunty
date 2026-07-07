using System;
using System.Data;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

public enum CatalogDatabaseProvider
{
    Sqlite,
    SqlServer,
    Postgres,
    MySql,
    MariaDb,
}

public static class CatalogSchema
{
    public static CatalogDatabaseProvider Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CatalogDatabaseProvider.SqlServer;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "sqlite" => CatalogDatabaseProvider.Sqlite,
            "sqlserver" or "mssql" => CatalogDatabaseProvider.SqlServer,
            "postgres" or "postgresql" or "npgsql" => CatalogDatabaseProvider.Postgres,
            "mysql" => CatalogDatabaseProvider.MySql,
            "mariadb" => CatalogDatabaseProvider.MariaDb,
            _ => CatalogDatabaseProvider.SqlServer,
        };
    }

    public static CatalogDatabaseProvider Detect(IDbConnection connection)
    {
        var name = connection.GetType().Name;

        if (name.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CatalogDatabaseProvider.Sqlite;
        }

        if (name.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CatalogDatabaseProvider.Postgres;
        }

        if (name.IndexOf("MySql", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return CatalogDatabaseProvider.MySql;
        }

        return CatalogDatabaseProvider.SqlServer;
    }

    public static void EnsureCreated(IDbConnection connection, CatalogDatabaseProvider provider)
    {
        switch (provider)
        {
            case CatalogDatabaseProvider.Sqlite:
                SqliteSchema.EnsureCreated(connection);
                break;
            case CatalogDatabaseProvider.Postgres:
                PostgresSchema.EnsureCreated(connection);
                break;
            case CatalogDatabaseProvider.MySql:
            case CatalogDatabaseProvider.MariaDb:
                MySqlSchema.EnsureCreated(connection);
                break;
            default:
                SqlServerSchema.EnsureCreated(connection);
                break;
        }
    }

    public static void EnsureCreated(IDbConnection connection)
        => EnsureCreated(connection, Detect(connection));

    public static void Reset(IDbConnection connection, CatalogDatabaseProvider provider)
    {
        switch (provider)
        {
            case CatalogDatabaseProvider.Sqlite:
                SqliteSchema.EnsureCreated(connection);
                break;
            case CatalogDatabaseProvider.Postgres:
                PostgresSchema.Reset(connection);
                break;
            case CatalogDatabaseProvider.MySql:
            case CatalogDatabaseProvider.MariaDb:
                MySqlSchema.Reset(connection);
                break;
            default:
                SqlServerSchema.Reset(connection);
                break;
        }
    }

    public static void Reset(IDbConnection connection)
        => Reset(connection, Detect(connection));
}
