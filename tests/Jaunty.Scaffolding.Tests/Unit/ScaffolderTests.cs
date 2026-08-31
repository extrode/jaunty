using Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class ScaffolderTests
{
    // ------------------------------------------------------------------
    // ScaffoldAsync / ValidateOptions
    // ------------------------------------------------------------------

    [Fact]
    public async Task ScaffoldAsync_NullOptions_ReportsTheMissingArgumentNotANullReference()
    {
        // AUD-R32-007 (re-found from round 27): the null used to be dereferenced inside the try,
        // so the catch-all returned "Object reference not set to an instance of an object." -
        // indistinguishable from a database failure and naming nothing the caller can act on.
        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(null!);

        Assert.False(result.Success);
        Assert.DoesNotContain("Object reference not set", result.Error, StringComparison.Ordinal);
        Assert.Contains("options", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScaffoldAsync_EmptyConnectionString_ReturnsFailedWithMessage()
    {
        var options = new ScaffoldOptions { ConnectionString = "", OutputDirectory = "./out", Namespace = "Generated" };

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(options);

        Assert.False(result.Success);
        Assert.StartsWith("Connection string is required.", result.Error);
    }

    [Fact]
    public async Task ScaffoldAsync_EmptyOutputDirectory_ReturnsFailedWithMessage()
    {
        var options = new ScaffoldOptions { ConnectionString = "Data Source=./app.db", OutputDirectory = "", Namespace = "Generated" };

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(options);

        Assert.False(result.Success);
        Assert.StartsWith("Output directory is required.", result.Error);
    }

    [Fact]
    public async Task ScaffoldAsync_EmptyNamespace_ReturnsFailedWithMessage()
    {
        var options = new ScaffoldOptions { ConnectionString = "Data Source=./app.db", OutputDirectory = "./out", Namespace = "" };

        ScaffoldResult result = await new Scaffolder().ScaffoldAsync(options);

        Assert.False(result.Success);
        Assert.StartsWith("Namespace is required.", result.Error);
    }

    // ------------------------------------------------------------------
    // DetectProvider
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Data Source=./app.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Filename=app.sqlite")]
    public void DetectProvider_SqliteConnectionStrings_DetectsSQLite(string connectionString)
    {
        var result = Scaffolder.DetectProvider(connectionString);
        Assert.Equal(DatabaseProvider.SQLite, result);
    }

    [Theory]
    [InlineData("Data Source=.;Initial Catalog=Foo;Trusted_Connection=True;")]
    [InlineData("Server=.;Database=Foo;Trusted_Connection=True;")]
    [InlineData("Data Source=.;Database=Foo;Trusted_Connection=True;")]
    [InlineData("Data Source=.;Database=Foo;User Id=sa;Password=x;")]
    public void DetectProvider_SqlServerConnectionStrings_DetectsSqlServer(string connectionString)
    {
        // "Data Source=" alone is SQLite's own signal, but a string that also carries
        // "Database=" (or "Initial Catalog=") plus SQL Server auth markers is SQL Server -
        // the SQLite rule must not shadow it just because "Initial Catalog=" is absent.
        var result = Scaffolder.DetectProvider(connectionString);
        Assert.Equal(DatabaseProvider.SqlServer, result);
    }

    [Fact]
    public void DetectProvider_PostgreSqlConnectionString_DetectsPostgreSql()
    {
        var result = Scaffolder.DetectProvider("Host=localhost;Database=app;Username=postgres;Password=x;");
        Assert.Equal(DatabaseProvider.PostgreSql, result);
    }

    [Fact]
    public void DetectProvider_NpgsqlHostWithUserIdAlias_DetectsPostgreSql()
    {
        // Npgsql accepts "User Id=" as an alias for its canonical "Username=" - this combined
        // with "Host=" and "Database=" is otherwise identical in shape to the SQL Server
        // Server=/Database=/User Id= pattern, so "Host=" must still route to PostgreSql.
        var result = Scaffolder.DetectProvider("Host=pg.example.com;Database=app;User Id=x;Password=y;");
        Assert.Equal(DatabaseProvider.PostgreSql, result);
    }

    [Fact]
    public void DetectProvider_NpgsqlServerAliasWithDefaultPort_DetectsPostgreSql()
    {
        // Npgsql also accepts "Server=" as an alias for "Host=", which makes this string
        // indistinguishable from a SQL Server connection string by key names alone - the
        // Postgres default port 5432 is the disambiguating signal here.
        var result = Scaffolder.DetectProvider("Server=pg.example.com;Port=5432;Database=app;User Id=x;Password=y;");
        Assert.Equal(DatabaseProvider.PostgreSql, result);
    }

    [Theory]
    [InlineData("Server=.;Database=Foo;Trusted_Connection=True;")]
    [InlineData("Data Source=.;Database=Foo;User Id=sa;Password=x;")]
    public void DetectProvider_AmbiguousSqlServerStringsWithoutPostgresSignals_StillDetectSqlServer(string connectionString)
    {
        // Guards the Postgres-detection reorder above: a genuine SQL Server connection string
        // using the common Server=/Database=/User Id= shape must not be reclassified just
        // because it shares keys with Npgsql - only an explicit "Host=" key or the Postgres
        // default port should tip it toward PostgreSql.
        var result = Scaffolder.DetectProvider(connectionString);
        Assert.Equal(DatabaseProvider.SqlServer, result);
    }

    [Fact]
    public void DetectProvider_MySqlConnectionString_DetectsMySql()
    {
        var result = Scaffolder.DetectProvider("Server=localhost;Database=app;Uid=root;Pwd=x;");
        Assert.Equal(DatabaseProvider.MySql, result);
    }
}
