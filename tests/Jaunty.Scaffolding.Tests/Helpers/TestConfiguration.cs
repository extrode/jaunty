using System.Text.Json;

namespace Jaunty.Scaffolding.Tests.Helpers;

/// <summary>
/// Loads test connection strings from appsettings.json or environment variables.
///
/// Resolution order:
/// 1. Environment variable (e.g., JAUNTY_TEST_SQLSERVER, JAUNTY_TEST_POSTGRESQL)
/// 2. appsettings.json in the test output directory
/// 3. null (test will be skipped)
///
/// Mirrors tests/Jaunty.Tests/Helpers/TestConfiguration.cs and reuses the same env var
/// names, so CI's existing SQL Server service container (which sets JAUNTY_TEST_SQLSERVER
/// for Jaunty.Tests) also enables SqlServerSchemaReaderTests here with no workflow changes.
/// </summary>
public static class TestConfiguration
{
    private static readonly Lazy<TestConnectionStrings> _connectionStrings = new Lazy<TestConnectionStrings>(Load);

    public static string SqlServerConnectionString => _connectionStrings.Value.SqlServer;
    public static string PostgreSqlConnectionString => _connectionStrings.Value.PostgreSql;
    public static string MySqlConnectionString => _connectionStrings.Value.MySql;

    public static bool HasSqlServer => !string.IsNullOrWhiteSpace(SqlServerConnectionString);
    public static bool HasPostgreSql => !string.IsNullOrWhiteSpace(PostgreSqlConnectionString);
    public static bool HasMySql => !string.IsNullOrWhiteSpace(MySqlConnectionString);

    private static TestConnectionStrings Load()
    {
        var result = new TestConnectionStrings();

        // Priority 1: Environment variables
        result.SqlServer = Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER") ?? "";
        result.PostgreSql = Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL") ?? "";
        result.MySql = Environment.GetEnvironmentVariable("JAUNTY_TEST_MYSQL") ?? "";
        var mariaDb = Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB") ?? "";

        // Priority 2: appsettings.json (if env vars not set)
        if (string.IsNullOrWhiteSpace(result.SqlServer) || string.IsNullOrWhiteSpace(result.PostgreSql) ||
            string.IsNullOrWhiteSpace(result.MySql) || string.IsNullOrWhiteSpace(mariaDb))
        {
            LoadFromAppSettings(result, ref mariaDb);
        }

        // MySqlSchemaReader is used for both MySQL and MariaDB (same wire protocol), so
        // either configuring the MySQL or the MariaDB connection string is enough.
        if (string.IsNullOrWhiteSpace(result.MySql))
            result.MySql = mariaDb;

        return result;
    }

    private static void LoadFromAppSettings(TestConnectionStrings result, ref string mariaDb)
    {
        // Walk up from the output directory to find appsettings.json
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        string? path = null;

        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "appsettings.json");
            if (File.Exists(candidate))
            {
                path = candidate;
                break;
            }
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        if (path == null) return;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings))
            {
                if (string.IsNullOrWhiteSpace(result.SqlServer) &&
                    connStrings.TryGetProperty("SqlServer", out var sqlServer))
                {
                    result.SqlServer = sqlServer.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(result.PostgreSql) &&
                    connStrings.TryGetProperty("PostgreSql", out var postgres))
                {
                    result.PostgreSql = postgres.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(result.MySql) &&
                    connStrings.TryGetProperty("MySql", out var mysql))
                {
                    result.MySql = mysql.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(mariaDb) &&
                    connStrings.TryGetProperty("MariaDb", out var mariadbEl))
                {
                    mariaDb = mariadbEl.GetString() ?? "";
                }
            }
        }
        catch
        {
            // Silently ignore parse errors — tests will just be skipped
        }
    }

    private class TestConnectionStrings
    {
        public string SqlServer { get; set; } = "";
        public string PostgreSql { get; set; } = "";
        public string MySql { get; set; } = "";
    }
}
