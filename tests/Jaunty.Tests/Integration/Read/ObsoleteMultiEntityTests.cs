using System.Data;
using System.Data.SQLite;

using Jaunty;
using Jaunty.Core;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests the [Obsolete] multi-entity overloads that take CommandOptions (non-generic)
/// instead of CommandOptions&lt;(T1, T2)&gt;. These are legacy APIs preserved for backward compatibility.
/// </summary>
#pragma warning disable CS0618 // Suppress obsolete warnings — these tests intentionally call obsolete methods
public class ObsoleteMultiEntityTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public ObsoleteMultiEntityTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        SeedData();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SeedData()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE orders (order_id INTEGER PRIMARY KEY, customer TEXT NOT NULL, total REAL NOT NULL);
            CREATE TABLE items (item_id INTEGER PRIMARY KEY, order_id INTEGER, product TEXT NOT NULL, qty INTEGER NOT NULL);

            INSERT INTO orders VALUES (1, 'Alice', 100.50);
            INSERT INTO orders VALUES (2, 'Bob', 200.75);
            INSERT INTO items VALUES (1, 1, 'Widget', 5);
            INSERT INTO items VALUES (2, 1, 'Gadget', 3);
            INSERT INTO items VALUES (3, 2, 'Doohickey', 1);";
        cmd.ExecuteNonQuery();
    }

    #region Test Entities

    public class OrderDto
    {
        public int OrderId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public double Total { get; set; }
    }

    public class ItemDto
    {
        public int ItemId { get; set; }
        public string Product { get; set; } = string.Empty;
        public int Qty { get; set; }
    }

    #endregion

    private const string JoinSql = @"
        SELECT o.order_id AS OrderId, o.customer AS Customer, o.total AS Total,
               i.item_id AS ItemId, i.product AS Product, i.qty AS Qty
        FROM orders o
        JOIN items i ON o.order_id = i.order_id";

    #region Obsolete Query<T1, T2>

    [Fact]
    public void ObsoleteQuery_ReturnsAllRows()
    {
        var results = Jaunty.Query<OrderDto, ItemDto>(
            _connection, JoinSql, parameters: null, options: default(CommandOptions));

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.True(r.Item1.OrderId > 0);
            Assert.NotEmpty(r.Item1.Customer);
            Assert.True(r.Item2.ItemId > 0);
            Assert.NotEmpty(r.Item2.Product);
        });
    }

    [Fact]
    public void ObsoleteQuery_WithParameters_FiltersResults()
    {
        var results = Jaunty.Query<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = @orderId",
            parameters: new { orderId = 1 },
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.Item1.OrderId));
    }

    [Fact]
    public void ObsoleteQuery_EmptyResult_ReturnsEmptyList()
    {
        var results = Jaunty.Query<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Empty(results);
    }

    #endregion

    #region Obsolete Query<T1, T2, TResult> (Combiner)

    [Fact]
    public void ObsoleteQueryWithCombiner_CombinesEntities()
    {
        var results = Jaunty.Query<OrderDto, ItemDto, string>(
            _connection,
            JoinSql + " WHERE o.order_id = 1 ORDER BY i.item_id",
            map: (order, item) => $"{order.Customer}:{item.Product}",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice:Widget", results[0]);
        Assert.Equal("Alice:Gadget", results[1]);
    }

    [Fact]
    public void ObsoleteQueryWithCombiner_NullMap_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Jaunty.Query<OrderDto, ItemDto, string>(
                _connection,
                JoinSql,
                map: null!,
                parameters: null,
                options: default(CommandOptions)));
    }

    #endregion

    #region Obsolete QueryFirst<T1, T2>

    [Fact]
    public void ObsoleteQueryFirst_ReturnsFirstRow()
    {
        var result = Jaunty.QueryFirst<OrderDto, ItemDto>(
            _connection,
            JoinSql + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(1, result.Item1.OrderId);
        Assert.Equal("Alice", result.Item1.Customer);
        Assert.Equal(1, result.Item2.ItemId);
        Assert.Equal("Widget", result.Item2.Product);
    }

    [Fact]
    public void ObsoleteQueryFirst_NoRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QueryFirst<OrderDto, ItemDto>(
                _connection,
                JoinSql + " WHERE o.order_id = 999",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("no elements", ex.Message);
    }

    #endregion

    #region Obsolete QueryFirstOrDefault<T1, T2>

    [Fact]
    public void ObsoleteQueryFirstOrDefault_ReturnsFirstRow()
    {
        var result = Jaunty.QueryFirstOrDefault<OrderDto, ItemDto>(
            _connection,
            JoinSql + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.OrderId);
    }

    [Fact]
    public void ObsoleteQueryFirstOrDefault_NoRows_ReturnsNull()
    {
        var result = Jaunty.QueryFirstOrDefault<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    #endregion

    #region Obsolete QuerySingle<T1, T2>

    [Fact]
    public void ObsoleteQuerySingle_ExactlyOneRow_ReturnsIt()
    {
        var result = Jaunty.QuerySingle<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, result.Item1.OrderId);
        Assert.Equal("Doohickey", result.Item2.Product);
    }

    [Fact]
    public void ObsoleteQuerySingle_NoRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingle<OrderDto, ItemDto>(
                _connection,
                JoinSql + " WHERE o.order_id = 999",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("no elements", ex.Message);
    }

    [Fact]
    public void ObsoleteQuerySingle_MultipleRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingle<OrderDto, ItemDto>(
                _connection,
                JoinSql + " WHERE o.order_id = 1",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("more than one element", ex.Message);
    }

    #endregion

    #region Obsolete QuerySingleOrDefault<T1, T2>

    [Fact]
    public void ObsoleteQuerySingleOrDefault_ExactlyOneRow_ReturnsIt()
    {
        var result = Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Item1.OrderId);
    }

    [Fact]
    public void ObsoleteQuerySingleOrDefault_NoRows_ReturnsNull()
    {
        var result = Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    [Fact]
    public void ObsoleteQuerySingleOrDefault_MultipleRows_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
                _connection,
                JoinSql + " WHERE o.order_id = 1",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("more than one element", ex.Message);
    }

    #endregion

    #region Obsolete QueryStream<T1, T2>

    [Fact]
    public void ObsoleteQueryStream_StreamsRows()
    {
        var results = Jaunty.QueryStream<OrderDto, ItemDto>(
            _connection,
            JoinSql + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("Widget", results[0].Item2.Product);
        Assert.Equal("Gadget", results[1].Item2.Product);
        Assert.Equal("Doohickey", results[2].Item2.Product);
    }

    [Fact]
    public void ObsoleteQueryStream_EmptyResult_YieldsNothing()
    {
        var results = Jaunty.QueryStream<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions)).ToList();

        Assert.Empty(results);
    }

    #endregion

    #region Obsolete Async Variants

    [Fact]
    public async Task ObsoleteQueryAsync_ReturnsAllRows()
    {
        var results = await Jaunty.QueryAsync<OrderDto, ItemDto>(
            _connection, JoinSql, parameters: null, options: default(CommandOptions));

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task ObsoleteQueryAsyncWithCombiner_CombinesEntities()
    {
        var results = await Jaunty.QueryAsync<OrderDto, ItemDto, string>(
            _connection,
            JoinSql + " WHERE o.order_id = 1 ORDER BY i.item_id",
            map: (order, item) => $"{order.Customer}:{item.Product}",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice:Widget", results[0]);
    }

    [Fact]
    public async Task ObsoleteQueryFirstAsync_ReturnsFirstRow()
    {
        var result = await Jaunty.QueryFirstAsync<OrderDto, ItemDto>(
            _connection,
            JoinSql + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(1, result.Item1.OrderId);
        Assert.Equal("Widget", result.Item2.Product);
    }

    [Fact]
    public async Task ObsoleteQueryFirstOrDefaultAsync_NoRows_ReturnsNull()
    {
        var result = await Jaunty.QueryFirstOrDefaultAsync<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    [Fact]
    public async Task ObsoleteQuerySingleAsync_ExactlyOneRow_ReturnsIt()
    {
        var result = await Jaunty.QuerySingleAsync<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal("Doohickey", result.Item2.Product);
    }

    [Fact]
    public async Task ObsoleteQuerySingleOrDefaultAsync_NoRows_ReturnsNull()
    {
        var result = await Jaunty.QuerySingleOrDefaultAsync<OrderDto, ItemDto>(
            _connection,
            JoinSql + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    #endregion
}
#pragma warning restore CS0618

