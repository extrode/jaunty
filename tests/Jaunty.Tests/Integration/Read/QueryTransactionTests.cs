using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryTransactionTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryTransactionTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithTransaction_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var categories = connection.Query(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            new CommandOptions<Category>(transaction: transaction));

        Assert.NotEmpty(categories);
        transaction.Rollback();
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithTransaction_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products",
            new CommandOptions(transaction));

        Assert.True(count > 0);
        transaction.Rollback();
    }
}



