using Extrode.Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// One <c>DetectProvider</c> case per key alias and per file extension, each built so that
/// dropping that alias or extension changes the answer.
/// </summary>
public class ScaffolderDetectProviderAliasTests
{
    [Theory]
    [InlineData("Data Source=x.db;Database=d", DatabaseProvider.SQLite)]
    [InlineData("DataSource=x.db;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Filename=x.db;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Data Source=x.db3;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Data Source=x.sqlite;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Data Source=x.sqlite3;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Data Source=x.db?mode=ro;Database=d", DatabaseProvider.SQLite)]
    [InlineData("Data Source=?x.db;Database=d", DatabaseProvider.SqlServer)]
    [InlineData("DataSource=app", DatabaseProvider.SQLite)]
    [InlineData("Filename=app", DatabaseProvider.SQLite)]
    [InlineData("Host=h;Database=d;UserId=u", DatabaseProvider.PostgreSql)]
    [InlineData("Host=h;Username=u", DatabaseProvider.SqlServer)]
    [InlineData("Server=s;Database=d;Trusted_Connection=True;Uid=u", DatabaseProvider.SqlServer)]
    [InlineData("Server=s;Database=d;Integrated Security=True;Uid=u", DatabaseProvider.SqlServer)]
    [InlineData("Server=s;Database=d;UserId=sa;Uid=u", DatabaseProvider.SqlServer)]
    [InlineData("Uid=u", DatabaseProvider.SqlServer)]
    [InlineData("Server=s;Uid=u", DatabaseProvider.SqlServer)]
    [InlineData("Database=d;User=u", DatabaseProvider.SqlServer)]
    public void EachAliasCarriesItsOwnWeight(string connectionString, DatabaseProvider expected)
        => Assert.Equal(expected, Scaffolder.DetectProvider(connectionString));
}
