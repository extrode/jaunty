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
    public void Query_WithTimeoutOption_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(60));

        Assert.NotEmpty(categories);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithCommandOptions_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var categories = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            CommandOptions.WithTimeout(30));

        Assert.NotEmpty(categories);
    }
}


