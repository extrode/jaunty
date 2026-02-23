using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories";

        var categories = await connection.QueryAsync<Category>(sql);

        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.False(string.IsNullOrEmpty(c.CategoryName)));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_WithNamedParameter_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @CategoryId"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId";

        var categories = await connection.QueryAsync<Category>(sql, new { CategoryId = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_NoRows_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

        var categories = await connection.QueryAsync<Category>(sql, new { Id = -999 });

        Assert.Empty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_StrictMode_MissingColumn_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection.QueryAsync<Category>(sql));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories";

        var categories = await connection.QueryAsync<Category>(sql, cts.Token);

        Assert.NotEmpty(categories);
    }
}


