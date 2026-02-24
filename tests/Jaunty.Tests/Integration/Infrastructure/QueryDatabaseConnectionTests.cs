using System.Data;

using FluentAssertions;

using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Infrastructure;

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
    public void TestDatabaseConnection(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        long result = connection.QueryScalar<long>("SELECT 1");
        result.Should().Be(1);
    }
}
