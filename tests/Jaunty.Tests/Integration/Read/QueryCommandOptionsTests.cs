using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryCommandOptionsTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryCommandOptionsTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithTimeoutOption_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(60));

        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithCommandOptions_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(30));

        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithParametersAndTimeout_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

        var categories = connection.Query<Category>(sql, new { Id = 1 }, CommandOptions<Category>.WithTimeout(30));

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_WithTimeoutOption_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories";

        var categories = await connection.QueryAsync<Category>(sql, CommandOptions<Category>.WithTimeout(60));

        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_WithParametersAndTimeout_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

        var categories = await connection.QueryAsync<Category>(sql, new { Id = 1 }, CommandOptions<Category>.WithTimeout(30));

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }
}


