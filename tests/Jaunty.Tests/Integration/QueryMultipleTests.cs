using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class QueryMultipleTests : IDisposable
{
    private readonly Database _db;

    public QueryMultipleTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryMultiple_ReadsMultipleResultSets_Buffered()
    {
        var sql = @"
        SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;
        SELECT customer_id, company_name FROM customers ORDER BY customer_id LIMIT 2;";

        List<Order>? orders = null;
        List<Customer>? customers = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            orders = [.. reader.Read<Order>()];
            customers = [.. reader.Read<Customer>()];
        });

        _db.Connection.QueryMultiple(sql);

        Assert.NotNull(orders);
        Assert.NotNull(customers);
        Assert.Equal(3, orders.Count);
        Assert.Equal(2, customers.Count);
    }

}
