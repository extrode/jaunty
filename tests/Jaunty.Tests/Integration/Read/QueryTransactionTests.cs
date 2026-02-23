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
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithTransaction_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();

        var categories = connection.Query(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT CategoryId, CategoryName, Description FROM Categories"
                : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            new CommandOptions<Category>(transaction: transaction));

        Assert.NotEmpty(categories);
        transaction.Rollback();
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    [Postgres]
    [SqlServer]
    [MariaDB]
    public void QueryScalar_WithTransaction_ExecutesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = connection.QueryScalar("SELECT COUNT(*) FROM products",
                CommandOptions<int>.WithTransaction(transaction));

            Assert.True(count > 0);
        }
        else
        {
            var count = connection.QueryScalar("SELECT COUNT(*) FROM products",
                CommandOptions<long>.WithTransaction(transaction));

            Assert.True(count > 0);
        }

        transaction.Rollback();
    }
}



