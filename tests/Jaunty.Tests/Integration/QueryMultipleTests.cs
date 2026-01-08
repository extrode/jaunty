using System.Data.Common;

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

    #region Sync Read Tests

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

    [Fact]
    public void ReadPartial_MapsSubsetOfColumns()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

        List<OrderSummary>? orders = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            orders = reader.ReadPartial<OrderSummary>();
        });

        Assert.NotNull(orders);
        Assert.Equal(5, orders.Count);
        Assert.All(orders, o => Assert.True(o.OrderId > 0));
    }

    #endregion

    #region ReadFirst Tests

    [Fact]
    public void ReadFirst_ReturnsFirstRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadFirst<Order>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Fact]
    public void ReadFirst_NoRows_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                reader.ReadFirst<Order>();
            });
        });
    }

    [Fact]
    public void ReadFirstOrDefault_ReturnsFirstRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadFirstOrDefault<Order>();
        });

        Assert.NotNull(order);
    }

    [Fact]
    public void ReadFirstOrDefault_NoRows_ReturnsNull()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadFirstOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Fact]
    public void ReadPartialFirst_ReturnsFirstRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirst<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Fact]
    public void ReadPartialFirst_NoRows_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialFirst<OrderSummary>();
            });
        });
    }

    [Fact]
    public void ReadPartialFirstOrDefault_ReturnsFirstRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
    }

    [Fact]
    public void ReadPartialFirstOrDefault_NoRows_ReturnsNull()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialFirstOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadSingle Tests

    [Fact]
    public void ReadSingle_ReturnsSingleRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadSingle<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public void ReadSingle_NoRows_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                reader.ReadSingle<Order>();
            });
        });
    }

    [Fact]
    public void ReadSingle_MultipleRows_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                reader.ReadSingle<Order>();
            });
        });
    }

    [Fact]
    public void ReadSingleOrDefault_ReturnsSingleRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadSingleOrDefault<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public void ReadSingleOrDefault_NoRows_ReturnsNull()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Order? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadSingleOrDefault<Order>();
        });

        Assert.Null(order);
    }

    [Fact]
    public void ReadPartialSingle_ReturnsSingleRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingle<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public void ReadPartialSingle_NoRows_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                reader.ReadPartialSingle<OrderSummary>();
            });
        });
    }

    [Fact]
    public void ReadPartialSingleOrDefault_ReturnsSingleRow()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public void ReadPartialSingleOrDefault_NoRows_ReturnsNull()
    {
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            order = reader.ReadPartialSingleOrDefault<OrderSummary>();
        });

        Assert.Null(order);
    }

    #endregion

    #region ReadScalar Tests

    [Fact]
    public void ReadScalar_ReturnsValue()
    {
        var sql = "SELECT COUNT(*) FROM orders;";

        long? count = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            count = reader.ReadScalar<long>();
        });

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

    [Fact]
    public void ReadScalar_NoRows_ReturnsDefault()
    {
        var sql = "SELECT order_id FROM orders WHERE order_id = -999;";

        long? orderId = null;

        _db.Connection.QueryMultiple(sql, reader =>
        {
            orderId = reader.ReadScalar<long>();
        });

        Assert.Equal(0, orderId);
    }

    #endregion

    #region ReadStream Tests

    [Fact]
    public void ReadStream_StreamsResults()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

        var orders = new List<Order>();

        _db.Connection.QueryMultiple(sql, reader =>
        {
            foreach (var order in reader.ReadStream<Order>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    [Fact]
    public void ReadPartialStream_StreamsResults()
    {
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

        var orders = new List<OrderSummary>();

        _db.Connection.QueryMultiple(sql, reader =>
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

    [Fact]
    public async Task QueryMultipleAsync_ReadsMultipleResultSets()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = @"
        SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;
        SELECT customer_id, company_name FROM customers ORDER BY customer_id LIMIT 2;";

        List<Order>? orders = null;
        List<Customer>? customers = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadAsync<Order>();
            customers = await reader.ReadAsync<Customer>();
        });

        Assert.NotNull(orders);
        Assert.NotNull(customers);
        Assert.Equal(3, orders.Count);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task ReadPartialAsync_MapsSubsetOfColumns()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

        List<OrderSummary>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadPartialAsync<OrderSummary>();
        });

        Assert.NotNull(orders);
        Assert.Equal(5, orders.Count);
    }

    [Fact]
    public async Task ReadFirstAsync_ReturnsFirstRow()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadFirstAsync<Order>();
        });

        Assert.NotNull(order);
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_NoRows_ReturnsNull()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadFirstOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Fact]
    public async Task ReadPartialFirstAsync_ReturnsFirstRow()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
    }

    [Fact]
    public async Task ReadPartialFirstOrDefaultAsync_NoRows_ReturnsNull()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialFirstOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Fact]
    public async Task ReadSingleAsync_ReturnsSingleRow()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadSingleAsync<Order>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public async Task ReadSingleOrDefaultAsync_NoRows_ReturnsNull()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadSingleOrDefaultAsync<Order>();
        });

        Assert.Null(order);
    }

    [Fact]
    public async Task ReadPartialSingleAsync_ReturnsSingleRow()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleAsync<OrderSummary>();
        });

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public async Task ReadPartialSingleOrDefaultAsync_NoRows_ReturnsNull()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = -999;";

        OrderSummary? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadPartialSingleOrDefaultAsync<OrderSummary>();
        });

        Assert.Null(order);
    }

    [Fact]
    public async Task ReadScalarAsync_ReturnsValue()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT COUNT(*) FROM orders;";

        long? count = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            count = await reader.ReadScalarAsync<long>();
        });

        Assert.NotNull(count);
        Assert.True(count > 0);
    }

    [Fact]
    public async Task ReadStreamAsync_StreamsResults()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

        var orders = new List<Order>();

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            await foreach (var order in reader.ReadStreamAsync<Order>())
            {
                orders.Add(order);
            }
        });

        Assert.Equal(5, orders.Count);
    }

    [Fact]
    public async Task ReadPartialStreamAsync_StreamsResults()
    {
        var conn = _db.Connection! as DbConnection;
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 5;";

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

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task ReadAsync_WithCancellationToken_Works()
    {
        var conn = _db.Connection! as DbConnection;
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        List<Order>? orders = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            orders = await reader.ReadAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(orders);
        Assert.Equal(3, orders.Count);
    }

    [Fact]
    public async Task ReadFirstAsync_WithCancellationToken_Works()
    {
        var conn = _db.Connection! as DbConnection;
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id, customer_id FROM orders ORDER BY order_id LIMIT 3;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadFirstAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
    }

    [Fact]
    public async Task ReadSingleAsync_WithCancellationToken_Works()
    {
        var conn = _db.Connection! as DbConnection;
        using var cts = new CancellationTokenSource();
        var sql = "SELECT order_id, customer_id FROM orders WHERE order_id = 10248;";

        Order? order = null;

        await conn!.QueryMultipleAsync(sql, async reader =>
        {
            order = await reader.ReadSingleAsync<Order>(cancellationToken: cts.Token);
        }, cancellationToken: cts.Token);

        Assert.NotNull(order);
        Assert.Equal(10248, order.OrderId);
    }

    [Fact]
    public async Task ReadScalarAsync_WithCancellationToken_Works()
    {
        var conn = _db.Connection! as DbConnection;
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

    [Fact]
    public void Read_AfterAllConsumed_Throws()
    {
        var sql = "SELECT order_id, customer_id FROM orders LIMIT 1;";

        Assert.Throws<InvalidOperationException>(() =>
        {
            _db.Connection.QueryMultiple(sql, reader =>
            {
                _ = reader.Read<Order>();
                _ = reader.Read<Order>(); // Should throw - already consumed
            });
        });
    }

    #endregion
}
