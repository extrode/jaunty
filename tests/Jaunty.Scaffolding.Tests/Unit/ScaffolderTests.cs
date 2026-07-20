using Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class ScaffolderTests
{
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
    public void DetectProvider_MySqlConnectionString_DetectsMySql()
    {
        var result = Scaffolder.DetectProvider("Server=localhost;Database=app;Uid=root;Pwd=x;");
        Assert.Equal(DatabaseProvider.MySql, result);
    }
}
