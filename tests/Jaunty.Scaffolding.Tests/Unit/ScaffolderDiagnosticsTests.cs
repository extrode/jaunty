using Jaunty.Scaffolding;
using Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R26 regressions for two ways the scaffolder misled the user before it ever reached a
/// database: provider detection that read the password, and a failure message that named the
/// reflection wrapper instead of the cause.
/// </summary>
public class ScaffolderDiagnosticsTests
{
    // ------------------------------------------------------------------
    // Provider detection must read keys, not the whole string
    // ------------------------------------------------------------------

    /// <summary>
    /// Measured before the fix: this string detected as SQLite, purely because ".db" appears in
    /// the password, and the scaffold then failed with "unable to open database file".
    /// </summary>
    [Fact]
    public void PasswordContainingADatabaseFileExtension_DoesNotHijackDetection()
    {
        Assert.Equal(
            DatabaseProvider.SqlServer,
            Scaffolder.DetectProvider("Server=prod;Initial Catalog=Sales;User Id=sa;Password=hunter2.dbx"));
    }

    [Theory]
    [InlineData("Server=prod;Initial Catalog=Sales;User Id=sa;Password=my.sqlite.pass")]
    [InlineData("Server=prod;Initial Catalog=Sales;User Id=sa;Password=a.db")]
    [InlineData("Server=prod;Initial Catalog=Sales;User Id=sa;Password=\"weird;value.db\"")]
    public void NoValueOtherThanTheDataSource_CanTipDetectionToSqlite(string connectionString)
        => Assert.Equal(DatabaseProvider.SqlServer, Scaffolder.DetectProvider(connectionString));

    /// <summary>
    /// The mirror case: a password that looks like another provider's key must not tip detection
    /// either. Before the fix "host=" anywhere in the blob satisfied the PostgreSQL rule.
    /// </summary>
    [Fact]
    public void PasswordContainingAnotherProvidersKey_DoesNotHijackDetection()
    {
        Assert.Equal(
            DatabaseProvider.MySql,
            Scaffolder.DetectProvider("Server=localhost;Database=app;Uid=root;Pwd=\"host=evil;Username=x\""));
    }

    /// <summary>
    /// The data-source value is the one place a file extension is meaningful, and it must still
    /// be honoured - this is what distinguishes a SQLite file from a SQL Server instance name.
    /// </summary>
    [Theory]
    [InlineData("Data Source=./app.db", DatabaseProvider.SQLite)]
    [InlineData("Data Source=/var/db/app.sqlite3", DatabaseProvider.SQLite)]
    [InlineData("Filename=app.sqlite", DatabaseProvider.SQLite)]
    [InlineData("Data Source=:memory:", DatabaseProvider.SQLite)]
    public void SqliteDataSourceValues_AreStillDetected(string connectionString, DatabaseProvider expected)
        => Assert.Equal(expected, Scaffolder.DetectProvider(connectionString));

    [Fact]
    public void UnparseableConnectionString_FallsBackRatherThanThrowing()
    {
        // Detection runs before any connection is opened, so it must not be the thing that
        // throws - opening is what reports a malformed string, and now does so usefully.
        Assert.Equal(DatabaseProvider.SqlServer, Scaffolder.DetectProvider("!!! not a connection string !!!"));
    }

    // ------------------------------------------------------------------
    // The failure message must name the cause
    // ------------------------------------------------------------------

    /// <summary>
    /// Measured before the fix: <c>Error</c> was exactly "Exception has been thrown by the target
    /// of an invocation." - the fixed text of <c>TargetInvocationException</c>, with the real
    /// cause discarded one level down.
    /// </summary>
    [Fact]
    public async Task MalformedConnectionString_ReportsTheProviderMessage_NotTheReflectionWrapper()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(
            new ScaffoldOptions
            {
                ConnectionString = "!!! not a connection string !!!",
                Provider = DatabaseProvider.SQLite,
                OutputDirectory = Path.Combine(Path.GetTempPath(), "jaunty-scaffold-should-not-exist"),
                Namespace = "N",
                DryRun = true
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("target of an invocation", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("initialization string", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AGenuineProviderError_IsStillReportedVerbatim()
    {
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(
            new ScaffoldOptions
            {
                ConnectionString = "Data Source=/nonexistent-directory/x.db;Mode=ReadOnly",
                Provider = DatabaseProvider.SQLite,
                OutputDirectory = Path.Combine(Path.GetTempPath(), "jaunty-scaffold-should-not-exist"),
                Namespace = "N",
                DryRun = true
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("unable to open database file", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
