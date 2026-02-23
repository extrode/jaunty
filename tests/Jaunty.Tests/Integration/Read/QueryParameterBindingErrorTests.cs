using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryParameterBindingErrorTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryParameterBindingErrorTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_MissingParameter_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id AND category_name = @Name",
                new { Id = 1 })); // Missing Name parameter

        Assert.Contains("@Name", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_ExtraParameter_ThrowsArgumentException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Extra parameters in the anonymous object cause an error
        var ex = Assert.Throws<ArgumentException>(() =>
            connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
                new { Id = 1, Extra1 = 2, Extra2 = 3 }));

        Assert.Contains("Unused parameter", ex.Message);
    }
}


