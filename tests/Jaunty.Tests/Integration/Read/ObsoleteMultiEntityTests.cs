using Jaunty.Core;
using Jaunty.Tests.Helpers;
using Jaunty.Tests.Helpers.Dialects;

using Microsoft.Data.SqlClient;

using MySql.Data.MySqlClient;

using Npgsql;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests the [Obsolete] multi-entity overloads that take CommandOptions (non-generic)
/// instead of CommandOptions&lt;(T1, T2)&gt;. These are legacy APIs preserved for backward compatibility.
/// </summary>
#pragma warning disable CS0618 // Suppress obsolete warnings — these tests intentionally call obsolete methods
public class ObsoleteMultiEntityTests
{
    private static IDbConnection CreateSeededConnection(DialectInfo dialect)
    {
        IDbConnection connection = dialect.Provider switch
        {
            DialectProvider.SqlServer => new SqlConnection(TestConfiguration.SqlServerConnectionString),
            DialectProvider.Postgres => new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString),
            DialectProvider.MariaDb => new MySqlConnection(TestConfiguration.MariaDbConnectionString),
            _ => throw new InvalidOperationException($"Unsupported dialect for obsolete multi-entity tests: {dialect.Provider}.")
        };
        connection.Open();
        SeedData(connection, dialect);
        return connection;
    }

    private static void SeedData(IDbConnection connection, DialectInfo dialect)
    {
        var orders = OrdersTable(dialect);
        var items = ItemsTable(dialect);
        var createPrefix = dialect.Provider switch
        {
            DialectProvider.Postgres => "CREATE TEMP TABLE",
            DialectProvider.MariaDb => "CREATE TEMPORARY TABLE",
            _ => "CREATE TABLE"
        };
        var dropOrders = dialect.Provider switch
        {
            DialectProvider.SqlServer => "IF OBJECT_ID('tempdb..#obsolete_orders') IS NOT NULL DROP TABLE #obsolete_orders;",
            DialectProvider.MariaDb => "DROP TEMPORARY TABLE IF EXISTS obsolete_orders;",
            _ => "DROP TABLE IF EXISTS obsolete_orders;"
        };
        var dropItems = dialect.Provider switch
        {
            DialectProvider.SqlServer => "IF OBJECT_ID('tempdb..#obsolete_items') IS NOT NULL DROP TABLE #obsolete_items;",
            DialectProvider.MariaDb => "DROP TEMPORARY TABLE IF EXISTS obsolete_items;",
            _ => "DROP TABLE IF EXISTS obsolete_items;"
        };
        var totalType = dialect.Provider switch
        {
            DialectProvider.Postgres => "DOUBLE PRECISION",
            DialectProvider.SqlServer => "FLOAT",
            _ => "DOUBLE"
        };

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            " + dropItems + @"
            " + dropOrders + @"
            " + createPrefix + " " + orders + @" (order_id INT PRIMARY KEY, customer VARCHAR(100) NOT NULL, total " + totalType + @" NOT NULL);
            " + createPrefix + " " + items + @" (item_id INT PRIMARY KEY, order_id INT, product VARCHAR(100) NOT NULL, qty INT NOT NULL);

            INSERT INTO " + orders + @" VALUES (1, 'Alice', 100.50);
            INSERT INTO " + orders + @" VALUES (2, 'Bob', 200.75);
            INSERT INTO " + items + @" VALUES (1, 1, 'Widget', 5);
            INSERT INTO " + items + @" VALUES (2, 1, 'Gadget', 3);
            INSERT INTO " + items + @" VALUES (3, 2, 'Doohickey', 1);";
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

    private static string JoinSql(DialectInfo dialect) => @"
        SELECT o.order_id AS OrderId, o.customer AS Customer, o.total AS Total,
               i.item_id AS ItemId, i.product AS Product, i.qty AS Qty
        FROM " + OrdersTable(dialect) + @" o
        JOIN " + ItemsTable(dialect) + @" i ON o.order_id = i.order_id";

    private static string OrdersTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "#obsolete_orders" : "obsolete_orders";

    private static string ItemsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "#obsolete_items" : "obsolete_items";

    #region Obsolete Query<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuery_ReturnsAllRows(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.Query<OrderDto, ItemDto>(
            connection, JoinSql(dialect), parameters: null, options: default(CommandOptions));

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.True(r.Item1.OrderId > 0);
            Assert.NotEmpty(r.Item1.Customer);
            Assert.True(r.Item2.ItemId > 0);
            Assert.NotEmpty(r.Item2.Product);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuery_WithParameters_FiltersResults(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.Query<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = @orderId",
            parameters: new { orderId = 1 },
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.Item1.OrderId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuery_EmptyResult_ReturnsEmptyList(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.Query<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Empty(results);
    }

    #endregion

    #region Obsolete Query<T1, T2, TResult> (Combiner)

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryWithCombiner_CombinesEntities(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.Query<OrderDto, ItemDto, string>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 1 ORDER BY i.item_id",
            map: (order, item) => $"{order.Customer}:{item.Product}",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice:Widget", results[0]);
        Assert.Equal("Alice:Gadget", results[1]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryWithCombiner_NullMap_Throws(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        Assert.Throws<ArgumentNullException>(() =>
            Jaunty.Query<OrderDto, ItemDto, string>(
                connection,
                JoinSql(dialect),
                map: null!,
                parameters: null,
                options: default(CommandOptions)));
    }

    #endregion

    #region Obsolete QueryFirst<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryFirst_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QueryFirst<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(1, result.Item1.OrderId);
        Assert.Equal("Alice", result.Item1.Customer);
        Assert.Equal(1, result.Item2.ItemId);
        Assert.Equal("Widget", result.Item2.Product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryFirst_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QueryFirst<OrderDto, ItemDto>(
                connection,
                JoinSql(dialect) + " WHERE o.order_id = 999",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("no elements", ex.Message);
    }

    #endregion

    #region Obsolete QueryFirstOrDefault<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryFirstOrDefault_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QueryFirstOrDefault<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryFirstOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QueryFirstOrDefault<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    #endregion

    #region Obsolete QuerySingle<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingle_ExactlyOneRow_ReturnsIt(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QuerySingle<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, result.Item1.OrderId);
        Assert.Equal("Doohickey", result.Item2.Product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingle_NoRows_Throws(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingle<OrderDto, ItemDto>(
                connection,
                JoinSql(dialect) + " WHERE o.order_id = 999",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingle_MultipleRows_Throws(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingle<OrderDto, ItemDto>(
                connection,
                JoinSql(dialect) + " WHERE o.order_id = 1",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("more than one element", ex.Message);
    }

    #endregion

    #region Obsolete QuerySingleOrDefault<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingleOrDefault_ExactlyOneRow_ReturnsIt(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Item1.OrderId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingleOrDefault_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQuerySingleOrDefault_MultipleRows_Throws(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Jaunty.QuerySingleOrDefault<OrderDto, ItemDto>(
                connection,
                JoinSql(dialect) + " WHERE o.order_id = 1",
                parameters: null,
                options: default(CommandOptions)));

        Assert.Contains("more than one element", ex.Message);
    }

    #endregion

    #region Obsolete QueryStream<T1, T2>

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryStream_StreamsRows(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.QueryStream<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("Widget", results[0].Item2.Product);
        Assert.Equal("Gadget", results[1].Item2.Product);
        Assert.Equal("Doohickey", results[2].Item2.Product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ObsoleteQueryStream_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = Jaunty.QueryStream<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions)).ToList();

        Assert.Empty(results);
    }

    #endregion

    #region Obsolete Async Variants

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQueryAsync_ReturnsAllRows(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = await Jaunty.QueryAsync<OrderDto, ItemDto>(
            connection, JoinSql(dialect), parameters: null, options: default(CommandOptions));

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQueryAsyncWithCombiner_CombinesEntities(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var results = await Jaunty.QueryAsync<OrderDto, ItemDto, string>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 1 ORDER BY i.item_id",
            map: (order, item) => $"{order.Customer}:{item.Product}",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(2, results.Count);
        Assert.Equal("Alice:Widget", results[0]);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQueryFirstAsync_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = await Jaunty.QueryFirstAsync<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " ORDER BY i.item_id",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal(1, result.Item1.OrderId);
        Assert.Equal("Widget", result.Item2.Product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQueryFirstOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = await Jaunty.QueryFirstOrDefaultAsync<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQuerySingleAsync_ExactlyOneRow_ReturnsIt(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = await Jaunty.QuerySingleAsync<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE i.item_id = 3",
            parameters: null,
            options: default(CommandOptions));

        Assert.Equal("Doohickey", result.Item2.Product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task ObsoleteQuerySingleOrDefaultAsync_NoRows_ReturnsNull(DialectInfo dialect)
    {
        using var connection = CreateSeededConnection(dialect);
        var result = await Jaunty.QuerySingleOrDefaultAsync<OrderDto, ItemDto>(
            connection,
            JoinSql(dialect) + " WHERE o.order_id = 999",
            parameters: null,
            options: default(CommandOptions));

        Assert.Null(result);
    }

    #endregion
}
#pragma warning restore CS0618



