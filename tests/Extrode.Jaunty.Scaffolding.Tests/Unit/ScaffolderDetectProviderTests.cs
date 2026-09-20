using Extrode.Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// mutation-gaps-2026-09-21: <c>Scaffolder.DetectProvider</c> (internal static, reachable here via
/// <c>InternalsVisibleTo</c>) had no test referencing it by name at all - live mutation testing
/// showed every <c>&amp;&amp;</c>/<c>||</c> in its four provider heuristics can be flipped without
/// any test failing. The method's own comments document real, deliberate ordering decisions (most
/// notably: PostgreSQL is checked before SQL Server because Npgsql accepts <c>Server=</c>/
/// <c>User Id=</c> as aliases for its own <c>Host=</c>/<c>Username=</c> keys, so a valid Npgsql
/// string can otherwise satisfy the SQL Server heuristic too) - these tests exercise exactly that
/// ambiguity, not just the easy unambiguous cases.
/// </summary>
public class ScaffolderDetectProviderTests
{
    // ------------------------------------------------------------------
    // SQLite - detected by file extension, or by having a data source and
    // nothing that looks like a server-hosted database name
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Data Source=app.db")]
    [InlineData("Data Source=app.db3")]
    [InlineData("Data Source=app.sqlite")]
    [InlineData("Data Source=app.sqlite3")]
    [InlineData("Data Source=APP.DB")] // extension match is case-insensitive
    public void RecognisedSqliteExtension_DetectsSqlite(string connectionString)
    {
        Assert.Equal(DatabaseProvider.SQLite, Scaffolder.DetectProvider(connectionString));
    }

    [Fact]
    public void DataSourceWithoutExtensionOrDatabaseName_FallsBackToSqliteHeuristic()
    {
        // No recognised extension, but also nothing suggesting a server-hosted database (no
        // "database"/"initial catalog") - the second, weaker SQLite heuristic.
        Assert.Equal(DatabaseProvider.SQLite, Scaffolder.DetectProvider("Data Source=somefile"));
    }

    [Fact]
    public void DataSourcePlusInitialCatalog_IsNotSqlite_ItIsTheClassicSqlServerDsn()
    {
        // "Data Source"/"Initial Catalog"/"Integrated Security" is a real, common SqlClient
        // connection-string shape - hasDataSource alone must not short-circuit to SQLite once a
        // server-hosted database name is also present.
        DatabaseProvider provider = Scaffolder.DetectProvider(
            "Data Source=myserver;Initial Catalog=mydb;Integrated Security=true");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    // ------------------------------------------------------------------
    // PostgreSQL - and the documented reason it is checked before SQL Server
    // ------------------------------------------------------------------

    [Fact]
    public void CanonicalNpgsqlConnectionString_DetectsPostgreSql()
    {
        DatabaseProvider provider = Scaffolder.DetectProvider(
            "Host=localhost;Database=mydb;Username=postgres");

        Assert.Equal(DatabaseProvider.PostgreSql, provider);
    }

    [Fact]
    public void NpgsqlAliasesThatAlsoSatisfyTheSqlServerHeuristic_StillDetectPostgreSql()
    {
        // Server=/User Id=/Port=5432 are all valid Npgsql aliases, and this string also happens to
        // satisfy the SQL Server heuristic below ((hasServer) && (hasDatabase) && (hasUserId)) - the
        // exact ambiguity the code comment calls out. Postgres must win because it is checked first.
        DatabaseProvider provider = Scaffolder.DetectProvider(
            "Server=localhost;Database=mydb;User Id=postgres;Port=5432");

        Assert.Equal(DatabaseProvider.PostgreSql, provider);
    }

    [Fact]
    public void DatabaseAndUserIdWithoutHostOrPostgresPort_IsNotPostgreSql()
    {
        // hasDatabase && hasUserId are both true, but neither "host" nor port 5432 is present -
        // the third conjunct must still gate the match. Falls through every other heuristic too
        // (no server/data source), landing on the SqlServer default.
        DatabaseProvider provider = Scaffolder.DetectProvider("Database=mydb;User Id=me");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    [Fact]
    public void HostAndUserIdWithoutDatabase_IsNotPostgreSql()
    {
        // hasUserId and hasHost are both true, but "database" is missing - the first conjunct
        // must still gate the match, not just be one of several alternatives.
        DatabaseProvider provider = Scaffolder.DetectProvider("Host=localhost;User Id=me");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    // ------------------------------------------------------------------
    // SQL Server - checked after PostgreSQL, ahead of MySQL
    // ------------------------------------------------------------------

    [Fact]
    public void ServerDatabaseAndTrustedConnection_DetectsSqlServer()
    {
        DatabaseProvider provider = Scaffolder.DetectProvider(
            "Server=localhost;Database=mydb;Trusted_Connection=true");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    [Fact]
    public void ServerWithoutDatabaseOrInitialCatalog_IsNotSqlServerViaThatHeuristic()
    {
        // hasServer and hasUserId are both true, but neither "database" nor "initial catalog" is
        // present - falls through to the SqlServer default regardless, but only because nothing
        // else matches either (MySQL also requires a database name).
        DatabaseProvider provider = Scaffolder.DetectProvider("Server=localhost;User Id=sa;Password=x");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    // ------------------------------------------------------------------
    // MySQL - checked last, after SQL Server, since both can specify server= and database=
    // ------------------------------------------------------------------

    [Fact]
    public void ServerDatabaseAndUid_DetectsMySql()
    {
        // "Uid" is not "User Id"/"UserId", so this does not satisfy the SQL Server heuristic's
        // hasUserId conjunct, and is checked after SQL Server precisely so a genuine SQL Server
        // connection string (which also has server=/database=) is not misclassified as MySQL.
        DatabaseProvider provider = Scaffolder.DetectProvider("Server=localhost;Database=mydb;Uid=root;Pwd=x");

        Assert.Equal(DatabaseProvider.MySql, provider);
    }

    [Fact]
    public void ServerAndUidWithoutDatabase_IsNotMySql()
    {
        // hasServer and uid are both true, but "database" is missing - falls through to the
        // SqlServer default.
        DatabaseProvider provider = Scaffolder.DetectProvider("Server=localhost;Uid=root;Pwd=x");

        Assert.Equal(DatabaseProvider.SqlServer, provider);
    }

    // ------------------------------------------------------------------
    // Nothing recognisable at all
    // ------------------------------------------------------------------

    [Fact]
    public void NoRecognisedKeys_DefaultsToSqlServer()
    {
        Assert.Equal(DatabaseProvider.SqlServer, Scaffolder.DetectProvider("Foo=bar"));
    }
}
