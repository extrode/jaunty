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
    [MicrosoftSqlite]
    public void QueryMultiple_ReadsMultipleResultSets_Buffered(DialectInfo dialect)
    {
        var sql = @"
        SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;
        SELECT customer_id AS CustomerId, company_name AS CompanyName FROM customers ORDER BY customer_id LIMIT 2;";

        List<Order>? orders = null;
        List<Customer>? customers = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            orders = reader.ReadPartial<Order>().ToList();
            customers = reader.ReadPartial<Customer>().ToList();
        });

        _fixture.GetDbConnection(dialect).QueryMultiple(sql);

        Assert.NotNull(orders);
        Assert.NotNull(customers);
        Assert.Equal(3, orders.Count);
        Assert.Equal(2, customers.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartial_MapsSubsetOfColumns(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

        List<OrderSummary>? orders = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
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
    [MicrosoftSqlite]
    public void ReadFirst_ReturnsFirstRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirst<Order>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadFirst_NoRows_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                reader.ReadPartialFirst<Order>();
            });
        });
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadFirstOrDefault_ReturnsFirstRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<Order>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadFirstOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialFirst_ReturnsFirstRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirst<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialFirst_NoRows_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                reader.ReadPartialFirst<OrderSummary>();
            });
        });
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialFirstOrDefault_ReturnsFirstRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialFirstOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadSingle Tests

    [Theory]
    [MicrosoftSqlite]
    public void ReadSingle_ReturnsSingleRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingle<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadSingle_NoRows_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<Order>();
            });
        });
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadSingle_MultipleRows_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<Order>();
            });
        });
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadSingleOrDefault_ReturnsSingleRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadSingleOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Order? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialSingle_ReturnsSingleRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingle<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialSingle_NoRows_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<OrderSummary>();
            });
        });
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialSingleOrDefault_ReturnsSingleRow(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialSingleOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadScalar Tests

    [Theory]
    [MicrosoftSqlite]
    public void ReadScalar_ReturnsValue(DialectInfo dialect)
    {
        var sql = "SELECT COUNT(*) FROM orders;";

        long? count = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            count = reader.ReadScalar<long>();
        });

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadScalar_NoRows_ReturnsDefault(DialectInfo dialect)
    {
        var sql = "SELECT order_id FROM orders WHERE order_id = -999;";

        long? orderId = null;

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            orderId = reader.ReadScalar<long>();
        });

        Assert.Equal(0, orderId);
    }

    #endregion

    #region ReadStream Tests

    [Theory]
    [MicrosoftSqlite]
    public void ReadStream_StreamsResults(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

        var orders = new List<Order>();

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
        {
            foreach (var order in reader.ReadPartialStream<Order>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    public void ReadPartialStream_StreamsResults(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

        var orders = new List<OrderSummary>();

        _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
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
    [MicrosoftSqlite]
    public async Task QueryMultipleAsync_ReadsMultipleResultSets(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = @"
        SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;
        SELECT customer_id AS CustomerId, company_name AS CompanyName FROM customers ORDER BY customer_id LIMIT 2;";

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
    [MicrosoftSqlite]
    public async Task ReadPartialAsync_MapsSubsetOfColumns(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

        List<OrderSummary>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<OrderSummary>();
        });

        Assert.NotNull(orders);
        Assert.Equal(5, orders.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadFirstAsync_ReturnsFirstRow(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<Order>();
        });

        Assert.NotNull(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadFirstOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadPartialFirstAsync_ReturnsFirstRow(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadPartialFirstOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadSingleAsync_ReturnsSingleRow(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadSingleOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadPartialSingleAsync_ReturnsSingleRow(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadPartialSingleOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadScalarAsync_ReturnsValue(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT COUNT(*) FROM orders;";

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
    [MicrosoftSqlite]
    public async Task ReadStreamAsync_StreamsResults(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

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
    [MicrosoftSqlite]
    public async Task ReadPartialStreamAsync_StreamsResults(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 5;";

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
    [MicrosoftSqlite]
    public async Task ReadAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        List<Order>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(orders);
        Assert.Equal(3, orders.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadFirstAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadSingleAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders WHERE order_id = 10248;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ReadScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        var conn = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();
        var sql = "SELECT COUNT(*) FROM orders;";

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
    [MicrosoftSqlite]
    public void Read_AfterAllConsumed_Throws(DialectInfo dialect)
    {
        var sql = "SELECT order_id AS OrderId, customer_id AS CustomerId FROM orders LIMIT 1;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _fixture.GetDbConnection(dialect).QueryMultiple(sql, reader =>
            {
                _ = reader.ReadPartial<Order>();
                _ = reader.ReadPartial<Order>(); // Should throw - already consumed
            });
        });
    }

    #endregion
}



