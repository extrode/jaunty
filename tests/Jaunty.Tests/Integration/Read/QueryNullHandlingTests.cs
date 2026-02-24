using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests for null handling and empty result scenarios.
/// </summary>
public class QueryNullHandlingTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryNullHandlingTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_WithNullInNonNullableColumn_HandlesGracefully(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT * FROM Products WHERE UnitPrice IS NULL"
            : "SELECT * FROM products WHERE unit_price IS NULL";

        // Should not throw, even with null values
        var products = connection.QueryPartial<Product>(sql);
        Assert.NotNull(products);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = 99999"
            : "SELECT category_id, category_name, description FROM categories WHERE category_id = 99999";

        var result = connection.QueryFirstOrDefault<Category>(sql);
        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QuerySingle_NoResults_ThrowsInvalidOperationException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = 99999"
            : "SELECT category_id, category_name, description FROM categories WHERE category_id = 99999";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.QuerySingle<Category>(sql));

        Assert.Contains("no elements", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingle_MultipleResults_ThrowsInvalidOperationException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories"
            : "SELECT category_id, category_name, description FROM categories";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.QueryPartialSingle<Category>(sql));

        Assert.Contains("more than one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
