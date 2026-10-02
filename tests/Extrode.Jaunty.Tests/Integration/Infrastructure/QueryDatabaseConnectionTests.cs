using System.Data;

using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Infrastructure;

public class QueryDatabaseConnectionTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryDatabaseConnectionTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_SelectOne_ReturnsOne(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        long result = connection.QueryScalar<long>("SELECT 1");
        Assert.Equal(1, result);
    }
}