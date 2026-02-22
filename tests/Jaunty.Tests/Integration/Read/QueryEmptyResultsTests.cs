using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryEmptyResultsTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryEmptyResultsTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_NoMatchingRows_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { @id = -99999 });

        Assert.Empty(results);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_EmptyTable_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // customer_customer_demo is typically empty in Northwind
        var results = connection.Query<CustomerCustomerDemo>(
            "SELECT customer_id AS CustomerId, customer_type_id AS CustomerTypeId FROM customer_customer_demo");

        Assert.Empty(results);
    }

    private class CustomerCustomerDemo
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerTypeId { get; set; } = string.Empty;
    }
}


