using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryLargeResultSetTests : IDisposable
{
    private readonly Database _db;

    public QueryLargeResultSetTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_LargeResultSet_HandlesCorrectly()
    {
        var orders = _db.Connection.Query<OrderSummary>(
            "SELECT order_id AS OrderId, customer_id AS CustomerId, employee_id AS EmployeeId FROM orders");

        Assert.True(orders.Count > 100); // Northwind has ~800 orders
    }

    private class OrderSummary
    {
        public long OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public long EmployeeId { get; set; }
    }
}
