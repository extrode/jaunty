using System.Data.Common;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Helpers.Dialects;

/// <summary>
/// Answers "is this engine actually there?" rather than "did someone configure a connection
/// string for it?", and remembers the answer for the life of the process.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TestConfiguration.HasSqlServer"/> and its siblings are string tests -
/// <c>!string.IsNullOrWhiteSpace(...)</c> - so a configured-but-stopped engine used to run every
/// dialect row and fail on connect. Measured 2026-08-30 with the local <c>MSSQLSERVER</c> service
/// stopped: <c>dotnet test Jaunty.slnx -c Release -f net10.0</c> reported 800 failures, every one
/// <c>dialect: SqlServer</c>, with the rest of the solution green. None of them was a defect.
/// </para>
/// <para>
/// The probe is the same shape <see cref="MicrosoftSqliteAttribute"/> has always used for its
/// native library: open a connection, catch, cache the verdict in a static. Caching is required
/// rather than an optimisation - a dialect attribute is constructed once per theory during
/// discovery, so an uncached probe would pay the connect timeout hundreds of times. A short
/// timeout is applied through each provider's own connection-string builder, because the keyword
/// differs per provider (<c>Connect Timeout</c>, <c>Timeout</c>, <c>Connection Timeout</c>) and
/// string-appending the wrong one throws.
/// </para>
/// <para>
/// Skipping is right on a developer box and wrong in CI, where a stopped service container must
/// not read as a green leg. <see cref="IsRequired"/> is that switch: with
/// <c>JAUNTY_REQUIRE_SQLSERVER</c> set, an unreachable engine fails loudly instead of skipping.
/// See <c>DialectDataAttributeBase</c> for how the two combine.
/// </para>
/// </remarks>
internal static class DialectReachability
{
    /// <summary>Seconds to wait before calling an engine unreachable.</summary>
    /// <remarks>
    /// Paid once per engine per process. Long enough for a cold container to answer, short enough
    /// that a developer with nothing running is not held up: measured at 3s against a dead port,
    /// the whole `Jaunty.Tests` discovery cost 3 probes and finished in the usual time.
    /// </remarks>
    private const int TimeoutSeconds = 5;

    private static readonly Lazy<Probe> _sqlServer = new(() => Open(
        () => new SqlConnection(new SqlConnectionStringBuilder(TestConfiguration.SqlServerConnectionString)
        {
            ConnectTimeout = TimeoutSeconds,
        }.ConnectionString)));

    private static readonly Lazy<Probe> _postgres = new(() => Open(
        () => new NpgsqlConnection(new NpgsqlConnectionStringBuilder(TestConfiguration.PostgreSqlConnectionString)
        {
            Timeout = TimeoutSeconds,
        }.ConnectionString)));

    private static readonly Lazy<Probe> _mariaDb = new(() => Open(
        () => new MySqlConnection(new MySqlConnectionStringBuilder(TestConfiguration.MariaDbConnectionString)
        {
            ConnectionTimeout = TimeoutSeconds,
        }.ConnectionString)));

    /// <summary>The environment variable that makes an unreachable SQL Server a failure.</summary>
    public const string RequireSqlServer = "JAUNTY_REQUIRE_SQLSERVER";

    /// <summary>The environment variable that makes an unreachable PostgreSQL a failure.</summary>
    public const string RequirePostgreSql = "JAUNTY_REQUIRE_POSTGRESQL";

    /// <summary>
    /// The environment variable that makes an unreachable MariaDB/MySQL a failure. One name for
    /// both, because <see cref="TestConfiguration"/> already aliases the two connection strings
    /// onto each other in either direction - a box with one configured has both.
    /// </summary>
    public const string RequireMySql = "JAUNTY_REQUIRE_MYSQL";

    public static bool IsSqlServerReachable => _sqlServer.Value.Succeeded;

    public static bool IsPostgreSqlReachable => _postgres.Value.Succeeded;

    public static bool IsMariaDbReachable => _mariaDb.Value.Succeeded;

    public static string? SqlServerError => _sqlServer.Value.Error;

    public static string? PostgreSqlError => _postgres.Value.Error;

    public static string? MariaDbError => _mariaDb.Value.Error;

    /// <summary>
    /// Whether <paramref name="variable"/> asks for this engine to be present.
    /// </summary>
    /// <remarks>
    /// "0", "false" and "no" read as not-required so a CI job can turn one engine off without
    /// deleting the line; anything else non-blank counts as set, because the common mistake is
    /// writing <c>JAUNTY_REQUIRE_SQLSERVER: true</c> and expecting it to work.
    /// </remarks>
    public static bool IsRequired(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return !value!.Trim().Equals("0", StringComparison.OrdinalIgnoreCase)
            && !value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)
            && !value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    private static Probe Open(Func<DbConnection> factory)
    {
        try
        {
            using DbConnection connection = factory();
            connection.Open();
            return new Probe(true, null);
        }
        catch (Exception ex)
        {
            return new Probe(false, ex.Message);
        }
    }

    private readonly struct Probe
    {
        public Probe(bool succeeded, string? error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }

        public string? Error { get; }
    }
}
