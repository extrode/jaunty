using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryLargeResultSetTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryLargeResultSetTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_LargeResultSet_HandlesCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var orders = connection.Query<OrderSummary>(
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



