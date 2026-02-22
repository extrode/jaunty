using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryEmptyResultsTests : IDisposable
{
    private readonly Database _db;

    public QueryEmptyResultsTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_NoMatchingRows_ReturnsEmptyList()
    {
        var results = _db.Connection.Query<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { @id = -99999 });

        Assert.Empty(results);
    }

    [Fact]
    public void Query_EmptyTable_ReturnsEmptyList()
    {
        // customer_customer_demo is typically empty in Northwind
        var results = _db.Connection.Query<CustomerCustomerDemo>(
            "SELECT customer_id AS CustomerId, customer_type_id AS CustomerTypeId FROM customer_customer_demo");

        Assert.Empty(results);
    }

    private class CustomerCustomerDemo
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerTypeId { get; set; } = string.Empty;
    }
}
