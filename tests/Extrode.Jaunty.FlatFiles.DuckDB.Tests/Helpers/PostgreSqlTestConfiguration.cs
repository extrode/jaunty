namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;

/// <summary>
/// The one live-engine connection string this test project needs: a target for
/// <c>ImportExecutor.ImportUsingDbBatchAsync</c>, whose <c>DbBatch</c> branch Sqlite's
/// <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> never takes
/// (<c>CanCreateBatch</c> is <see langword="false"/> there). Mirrors
/// <c>Extrode.Jaunty.Tests.Helpers.TestConfiguration</c>'s resolution order, trimmed to the one
/// engine this project targets rather than mirroring all four.
/// </summary>
internal static class PostgreSqlTestConfiguration
{
    /// <summary>Makes an unreachable PostgreSQL a failure instead of a skip; same convention as
    /// <c>Extrode.Jaunty.Tests.Helpers.Dialects.DialectReachability.RequirePostgreSql</c>.</summary>
    public const string RequirePostgreSqlVariable = "JAUNTY_REQUIRE_POSTGRESQL";

    private static readonly Lazy<string> _connectionString = new(Load);

    // Always non-empty by design (Load falls back to the torture-postgres default), unlike
    // TestConfiguration's four-engine siblings which default to "" and skip on that alone.
    // Reachability - not configuration - is what OpenOrSkip actually needs to test here, so there
    // is deliberately no "not configured" skip path to keep in sync with this.
    public static string ConnectionString => _connectionString.Value;

    public static bool IsRequired
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(RequirePostgreSqlVariable);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return !value!.Trim().Equals("0", StringComparison.OrdinalIgnoreCase)
                && !value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)
                && !value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Load()
    {
        string? fromEnv = Environment.GetEnvironmentVariable("JAUNTY_TEST_POSTGRESQL");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        // Default matches the torture-postgres docker container documented in
        // scripts/reset-test-databases.ps1 and tests/Extrode.Jaunty.Scaffolding.Tests/appsettings.json.
        return "Host=localhost;Port=5433;Database=northwind;Username=postgres;Password=Torture_Test_Pwd1!;";
    }
}
