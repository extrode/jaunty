using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QuerySqlErrorTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QuerySqlErrorTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_InvalidSql_ThrowsException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.ThrowsAny<Exception>(() =>
            connection.Query<Category>("SELECT * FROM nonexistent_table"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_SyntaxError_ThrowsException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.ThrowsAny<Exception>(() =>
            connection.Query<Category>("SELEC * FRO categories"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_InvalidSql_ThrowsException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.ThrowsAny<Exception>(() =>
            connection.QueryScalar<int>("SELECT COUNT(*) FROM nonexistent_table"));
    }
}