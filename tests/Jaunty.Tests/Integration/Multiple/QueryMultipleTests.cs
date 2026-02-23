using System.Data.Common;

using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Multiple;

public class QueryMultipleTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryMultipleTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }
#region Sync Read Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryMultiple_ReadsMultipleResultSets_Buffered(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = MultipleOrdersCustomersSql(dialect);

        List<Order>? orders = null;
        List<Customer>? customers = null;

        connection.QueryMultiple(sql, reader =>
        {
            orders = reader.ReadPartial<Order>().ToList();
            customers = reader.ReadPartial<Customer>().ToList();
        });

        connection.QueryMultiple(sql);

        Assert.NotNull(orders);
        Assert.NotNull(customers);
        Assert.Equal(3, orders.Count);
        Assert.Equal(2, customers.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartial_MapsSubsetOfColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        List<OrderSummary>? orders = null;

        connection.QueryMultiple(sql, reader =>
        {
            orders = reader.ReadPartial<OrderSummary>();
        });

        Assert.NotNull(orders);
        Assert.Equal(5, orders.Count);
        Assert.All(orders, o => Assert.True(o.OrderId > 0));
    }

    #endregion

    #region ReadFirst Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadFirst_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirst<Order>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadFirst_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialFirst<Order>();
            });
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadFirstOrDefault_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<Order>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadFirstOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialFirst_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirst<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialFirst_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialFirst<OrderSummary>();
            });
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialFirstOrDefault_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialFirstOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadSingle Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadSingle_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingle<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadSingle_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<Order>();
            });
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadSingle_MultipleRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<Order>();
            });
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadSingleOrDefault_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadSingleOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Order? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialSingle_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingle<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialSingle_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<OrderSummary>();
            });
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialSingleOrDefault_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialSingleOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        OrderSummary? order = null;

        connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadScalar Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadScalar_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = CountOrdersSql(dialect);

        long? count = null;

        connection.QueryMultiple(sql, reader =>
        {
            count = reader.ReadScalar<long>();
        });

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadScalar_NoRows_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrderIdNoRowsSql(dialect);

        long? orderId = null;

        connection.QueryMultiple(sql, reader =>
        {
            orderId = reader.ReadScalar<long>();
        });

        Assert.Equal(0, orderId);
    }

    #endregion

    #region ReadStream Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadStream_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        var orders = new List<Order>();

        connection.QueryMultiple(sql, reader =>
        {
            foreach (var order in reader.ReadPartialStream<Order>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ReadPartialStream_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        var orders = new List<OrderSummary>();

        connection.QueryMultiple(sql, reader =>
        {
            foreach (var order in reader.ReadPartialStream<OrderSummary>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    #endregion

    #region Async Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryMultipleAsync_ReadsMultipleResultSets(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = MultipleOrdersCustomersSql(dialect);

        List<Order>? orders = null;
        List<Customer>? customers = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<Order>();
            customers = await reader.ReadPartialAsync<Customer>();
        });

        Assert.NotNull(orders);
        Assert.NotNull(customers);
        Assert.Equal(3, orders.Count);
        Assert.Equal(2, customers.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialAsync_MapsSubsetOfColumns(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        List<OrderSummary>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<OrderSummary>();
        });

        Assert.NotNull(orders);
        Assert.Equal(5, orders.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadFirstAsync_ReturnsFirstRow(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<Order>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadFirstOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialFirstAsync_ReturnsFirstRow(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 3);

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialFirstOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadSingleAsync_ReturnsSingleRow(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadSingleOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialSingleAsync_ReturnsSingleRow(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, 10248);

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialSingleOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersByOrderIdSql(dialect, -999);

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadScalarAsync_ReturnsValue(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = CountOrdersSql(dialect);

        long? count = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            count = await reader.ReadScalarAsync<long>();
        });

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadStreamAsync_StreamsResults(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        var orders = new List<Order>();

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            await foreach (var order in reader.ReadPartialStreamAsync<Order>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadPartialStreamAsync_StreamsResults(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 5);

        var orders = new List<OrderSummary>();

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            await foreach (var order in reader.ReadPartialStreamAsync<OrderSummary>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }
#endif

    #endregion

    #region CancellationToken Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = OrdersSql(dialect, 3);

        List<Order>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(orders);
        Assert.Equal(3, orders.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadFirstAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = OrdersSql(dialect, 3);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadSingleAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = OrdersByOrderIdSql(dialect, 10248);

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ReadScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = CountOrdersSql(dialect);

        long? count = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            count = await reader.ReadScalarAsync<long>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

    #endregion

    #region Consumed State Tests

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Read_AfterAllConsumed_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = OrdersSql(dialect, 1, orderBy: false);

        Assert.Throws<InvalidOperationException>(() =>
        {
            connection.QueryMultiple(sql, reader =>
            {
                _ = reader.ReadPartial<Order>();
                _ = reader.ReadPartial<Order>(); // Should throw - already consumed
            });
        });
    }

    #endregion

    private static string OrdersSql(DialectInfo dialect, int top, bool orderBy = true) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({top}) OrderId, CustomerId FROM Orders{(orderBy ? " ORDER BY OrderId" : string.Empty)};"
            : $"SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders{(orderBy ? " ORDER BY order_id" : string.Empty)} LIMIT {top};";

    private static string OrdersByOrderIdSql(DialectInfo dialect, int orderId) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT OrderId, CustomerId FROM Orders WHERE OrderId = {orderId};"
            : $"SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = {orderId};";

    private static string CountOrdersSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT COUNT(*) FROM Orders;"
            : "SELECT COUNT(*) FROM orders;";

    private static string OrderIdNoRowsSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT OrderId FROM Orders WHERE OrderId = -999;"
            : "SELECT order_id FROM orders WHERE order_id = -999;";

    private static string MultipleOrdersCustomersSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? @"
        SELECT TOP (3) OrderId, CustomerId FROM Orders ORDER BY OrderId;
        SELECT TOP (2) CustomerId, CompanyName FROM Customers ORDER BY CustomerId;"
            : @"
        SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;
        SELECT customer_id AS CustomerId, company_name AS CompanyName FROM customers ORDER BY customer_id LIMIT 2;";
}



