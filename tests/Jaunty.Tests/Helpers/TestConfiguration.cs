using System;
using System.IO;

#if NET8_0_OR_GREATER
using System.Text.Json;
#endif

namespace Jaunty.Tests.Helpers;

/// <summary>
/// Loads test connection strings from appsettings.json or environment variables.
///
/// Resolution order:
/// 1. Environment variable (e.g., JAUNTY_TEST_SQLSERVER, JAUNTY_TEST_POSTGRESQL)
/// 2. appsettings.json in the test output directory
/// 3. null (test will be skipped)
/// </summary>
public static class TestConfiguration
{
    private static readonly Lazy<TestConnectionStrings> _connectionStrings = new Lazy<TestConnectionStrings>(Load);

    public static string SqlServerConnectionString => _connectionStrings.Value.SqlServer;
    public static string PostgreSqlConnectionString => _connectionStrings.Value.PostgreSql;
    public static string MySqlConnectionString => _connectionStrings.Value.MySql;
    public static string MariaDbConnectionString => _connectionStrings.Value.MariaDb;

    public static bool HasSqlServer => !string.IsNullOrWhiteSpace(SqlServerConnectionString);
    public static bool HasPostgreSql => !string.IsNullOrWhiteSpace(PostgreSqlConnectionString);
    public static bool HasMySql => !string.IsNullOrWhiteSpace(MySqlConnectionString);
    public static bool HasMariaDb => !string.IsNullOrWhiteSpace(MariaDbConnectionString);

    private static TestConnectionStrings Load()
    {
        var result = new TestConnectionStrings();

        // Priority 1: Environment variables
        result.SqlServer = Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER") ?? "";
        result.PostgreSql = Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL") ?? "";
        result.MySql = Environment.GetEnvironmentVariable("JAUNTY_TEST_MYSQL") ?? "";
        result.MariaDb = Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB") ?? "";

        // Priority 2: appsettings.json (if env vars not set)
        if (string.IsNullOrWhiteSpace(result.SqlServer) || string.IsNullOrWhiteSpace(result.PostgreSql) || string.IsNullOrWhiteSpace(result.MySql) || string.IsNullOrWhiteSpace(result.MariaDb))
        {
            LoadFromAppSettings(result);
        }

        // Fallback aliases: allow either MySql or MariaDb to configure both.
        if (string.IsNullOrWhiteSpace(result.MariaDb))
        {
            result.MariaDb = result.MySql;
        }

        if (string.IsNullOrWhiteSpace(result.MySql))
        {
            result.MySql = result.MariaDb;
        }

        return result;
    }

    private static void LoadFromAppSettings(TestConnectionStrings result)
    {
        // Walk up from the output directory to find appsettings.json
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        string path = null;

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

#if NET8_0_OR_GREATER
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

                if (string.IsNullOrWhiteSpace(result.MariaDb) &&
                    connStrings.TryGetProperty("MariaDb", out var mariadb))
                {
                    result.MariaDb = mariadb.GetString() ?? "";
                }
            }
        }
        catch
        {
            // Silently ignore parse errors — tests will just be skipped
        }
#else
        // net472: Simple manual JSON parsing (no System.Text.Json available)
        try
        {
            var json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(result.SqlServer))
                result.SqlServer = ExtractJsonValue(json, "SqlServer");

            if (string.IsNullOrWhiteSpace(result.PostgreSql))
                result.PostgreSql = ExtractJsonValue(json, "PostgreSql");

            if (string.IsNullOrWhiteSpace(result.MySql))
                result.MySql = ExtractJsonValue(json, "MySql");

            if (string.IsNullOrWhiteSpace(result.MariaDb))
                result.MariaDb = ExtractJsonValue(json, "MariaDb");
        }
        catch
        {
            // Silently ignore parse errors
        }
#endif
    }

#if !NET8_0_OR_GREATER
    /// <summary>
    /// Minimal JSON value extractor for net472 (no System.Text.Json).
    /// Finds "key": "value" patterns. Not a full JSON parser.
    /// </summary>
    private static string ExtractJsonValue(string json, string key)
    {
        var searchKey = "\"" + key + "\"";
        var keyIndex = json.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return null;

        var colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
        if (colonIndex < 0) return null;

        var quoteStart = json.IndexOf('"', colonIndex + 1);
        if (quoteStart < 0) return null;

        var quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteEnd < 0) return null;

        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }
#endif

    private class TestConnectionStrings
    {
        public string SqlServer { get; set; } = "";
        public string PostgreSql { get; set; } = "";
        public string MySql { get; set; } = "";
        public string MariaDb { get; set; } = "";
    }
}
