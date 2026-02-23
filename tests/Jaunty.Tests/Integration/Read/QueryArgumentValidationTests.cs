using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryArgumentValidationTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryArgumentValidationTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.Query<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_NullSql_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Query<Category>(null!));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_EmptySql_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<Category>(""));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WhitespaceSql_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<Category>("   \t\n  "));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        IDbConnection? nullConnection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            nullConnection!.QueryScalar<int>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_NullSql_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryScalar<int>(null!));
    }
}


