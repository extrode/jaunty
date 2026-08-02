using System.Data.SQLite;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests multi-entity query operations through the IDbConnection fallback path.
/// Covers QueryMultiEntityCore, QueryFirstOrDefaultMultiEntityCore, QuerySingleOrDefaultMultiEntityCore,
/// and QueryStreamMultiEntityCore IDbConnection else branches.
/// </summary>
public class MultiEntityFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public MultiEntityFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    [Fact]
    public void QueryMultiEntity_ViaWrapper_ReturnsResults()
    {
        var results = _wrapper.Query<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE c.category_id = 1 ORDER BY o.order_id LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.True(r.Item1.OrderId > 0);
            Assert.NotNull(r.Item2.CategoryName);
        });
    }

    [Fact]
    public void QueryFirstMultiEntity_ViaWrapper_ReturnsFirst()
    {
        var result = _wrapper.QueryFirst<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE c.category_id = 1 ORDER BY o.order_id LIMIT 3");

        Assert.True(result.Item1.OrderId > 0);
        Assert.NotNull(result.Item2.CategoryName);
    }

    [Fact]
    public void QueryFirstOrDefaultMultiEntity_ViaWrapper_NoResults_ReturnsNull()
    {
        var result = _wrapper.QueryFirstOrDefault<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE o.order_id = @Id AND c.category_id = 1",
            new { Id = -999 });

        Assert.Null(result);
    }

    [Fact]
    public void QuerySingleMultiEntity_ViaWrapper_ReturnsSingle()
    {
        var result = _wrapper.QuerySingle<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE o.order_id = @OrderId AND c.category_id = @CatId",
            new { OrderId = 10248, CatId = 1 });

        Assert.Equal(10248, result.Item1.OrderId);
        Assert.NotNull(result.Item2.CategoryName);
    }

    [Fact]
    public void QuerySingleOrDefaultMultiEntity_ViaWrapper_MultipleResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _wrapper.QuerySingleOrDefault<OrderSummary, CategorySummary>(
                "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE c.category_id = 1 ORDER BY o.order_id LIMIT 2"));
    }

    [Fact]
    public void QueryStreamMultiEntity_ViaWrapper_YieldsResults()
    {
        var count = 0;
        foreach (var (order, category) in _wrapper.QueryStream<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE c.category_id = 1 ORDER BY o.order_id LIMIT 3"))
        {
            Assert.True(order.OrderId > 0);
            Assert.NotNull(category.CategoryName);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryStreamMultiEntity_ViaWrapper_EmptyResult_YieldsNothing()
    {
        var count = 0;
        foreach (var _ in _wrapper.QueryStream<OrderSummary, CategorySummary>(
            "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE o.order_id = @Id AND c.category_id = 1",
            new { Id = -999 }))
        {
            count++;
        }

        Assert.Equal(0, count);
    }
}