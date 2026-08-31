using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Microsoft.eShopWeb.FunctionalTests;

public static class TortureDatabase
{
    public const string EnvVar = "JAUNTY_TORTURE_DB";

    public const string MsSqlConnStrEnvVar = "JAUNTY_TORTURE_CONNSTR_MSSQL";
    public const string PostgresConnStrEnvVar = "JAUNTY_TORTURE_CONNSTR_POSTGRES";
    public const string MySqlConnStrEnvVar = "JAUNTY_TORTURE_CONNSTR_MYSQL";
    public const string MariaDbConnStrEnvVar = "JAUNTY_TORTURE_CONNSTR_MARIADB";

    private const string MsSqlFallback =
        "Server=localhost,1433;Database=EShopTortureTest;User Id=sa;TrustServerCertificate=True;";

    private const string PostgresFallback =
        "Host=localhost;Port=5432;Username=postgres;Database=torture_db";

    private const string MySqlFallback =
        "Server=localhost;Port=3306;Uid=root;Database=torture_db";

    private const string MariaDbFallback =
        "Server=localhost;Port=3307;Uid=root;Database=torture_db";

    private static string Resolve(string key, string fallback)
        => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    public static CatalogDatabaseProvider SelectedProvider()
        => CatalogSchema.Parse(NormalizeSqliteDefault(Environment.GetEnvironmentVariable(EnvVar)));

    public static IDbConnection CreateOpenConnection(string sqliteDataSource)
    {
        var provider = SelectedProvider();
        IDbConnection connection = provider switch
        {
            CatalogDatabaseProvider.SqlServer => new SqlConnection(Resolve(MsSqlConnStrEnvVar, MsSqlFallback)),
            CatalogDatabaseProvider.Postgres => new NpgsqlConnection(Resolve(PostgresConnStrEnvVar, PostgresFallback)),
            CatalogDatabaseProvider.MySql => new MySqlConnection(Resolve(MySqlConnStrEnvVar, MySqlFallback)),
            CatalogDatabaseProvider.MariaDb => new MySqlConnection(Resolve(MariaDbConnStrEnvVar, MariaDbFallback)),
            _ => new SqliteConnection($"DataSource={sqliteDataSource};Mode=Memory;Cache=Shared"),
        };

        connection.Open();

        if (provider == CatalogDatabaseProvider.Sqlite)
        {
            SqliteSchema.EnsureCreated(connection);
        }
        else
        {
            CatalogSchema.Reset(connection, provider);
        }

        return connection;
    }

    private static string? NormalizeSqliteDefault(string? value)
        => string.IsNullOrWhiteSpace(value) ? "sqlite" : value;
}
