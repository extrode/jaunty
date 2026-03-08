using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryCaseSensitivityTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryCaseSensitivityTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ColumnNameCaseInsensitive_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Tests case-insensitive matching in MetadataCache
        var categories = connection.Query<Category>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId AS categoryid, CategoryName AS categoryname, Description AS DESCRIPTION FROM Categories WHERE CategoryId = @Id"
                : "SELECT category_id AS categoryid, category_name AS categoryname, description AS DESCRIPTION FROM categories WHERE category_id = @Id",
             new { Id = 1 });

        Assert.Single(categories);
        Assert.False(string.IsNullOrEmpty(categories[0].CategoryName));
    }
}