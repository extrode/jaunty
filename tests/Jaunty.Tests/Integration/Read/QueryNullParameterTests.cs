using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests for null parameter validation across Query public APIs.
/// </summary>
public class QueryNullParameterTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryNullParameterTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Query Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Query<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Query<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_EmptySql_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<Category>(""));

        Assert.Contains("sql", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QueryAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryFirst Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryFirst<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryFirst<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryFirstAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QueryFirstAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QuerySingle Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QuerySingle_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QuerySingle<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QuerySingle_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QuerySingle<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QuerySingleAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QuerySingleAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryFirstOrDefault Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirstOrDefault_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryFirstOrDefault<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirstOrDefault_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryFirstOrDefault<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryFirstOrDefaultAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QueryFirstOrDefaultAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryPartial Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryPartial<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryPartial<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QueryPartialAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryScalar Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryScalar<int>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.QueryScalar<int>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryScalarAsync_NullSql_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.QueryScalarAsync<int>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion
}